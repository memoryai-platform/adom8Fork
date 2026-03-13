import { Link, useOutletContext } from 'react-router-dom';
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip as RechartsTooltip,
  XAxis,
  YAxis,
} from 'recharts';

import AgentActivityFeed from '../components/AgentActivityFeed';
import AgentCard from '../components/AgentCard';
import MetricCard from '../components/MetricCard';
import StoryQueueTable from '../components/StoryQueueTable';
import { AGENT_ORDER } from '../constants';
import { formatDuration, formatPercent, formatRelativeTime } from '../utils/formatting';

function resolveLatestTimestamp(workItemId, recentActivity) {
  const timestamps = (recentActivity ?? [])
    .filter((entry) => entry.workItemId === workItemId)
    .map((entry) => entry.timestamp)
    .filter(Boolean)
    .sort((left, right) => new Date(right).getTime() - new Date(left).getTime());

  return timestamps[0] ?? null;
}

function resolveLatestFailureEntry(workItemId, recentActivity) {
  return (recentActivity ?? [])
    .filter((entry) => entry.workItemId === workItemId && String(entry.level ?? '').toLowerCase() === 'error')
    .sort((left, right) => new Date(right.timestamp).getTime() - new Date(left.timestamp).getTime())[0] ?? null;
}

function normalizeAgentStatus(status) {
  switch (status) {
    case 'in_progress':
    case 'awaiting_code':
      return 'active';
    case 'completed':
      return 'completed';
    case 'failed':
    case 'needs_revision':
      return 'failed';
    default:
      return 'idle';
  }
}

function buildAgentCardsFromStories(stories, recentActivity) {
  return AGENT_ORDER.map((agentName) => {
    const agentKey = agentName.replace('Agent', '');
    const matchingStories = (stories ?? []).filter((story) => story?.agents?.[agentKey] || story?.agents?.[agentName]);
    const latestActivity = (recentActivity ?? [])
      .filter((entry) => entry.agent === agentKey || entry.agent === agentName)
      .sort((left, right) => new Date(right.timestamp).getTime() - new Date(left.timestamp).getTime())[0] ?? null;
    const activeStory = matchingStories.find((story) => ['in_progress', 'awaiting_code'].includes(story?.agents?.[agentKey] ?? story?.agents?.[agentName]));
    const failedStory = matchingStories.find((story) => ['failed', 'needs_revision'].includes(story?.agents?.[agentKey] ?? story?.agents?.[agentName]));
    const completedStories = matchingStories.filter((story) => (story?.agents?.[agentKey] ?? story?.agents?.[agentName]) === 'completed');

    return {
      name: agentName,
      status: activeStory ? 'active' : failedStory ? 'failed' : completedStories.length ? 'completed' : 'idle',
      lastRun: latestActivity?.timestamp ?? null,
      storiesProcessed: completedStories.length,
      avgDurationSeconds:
        completedStories
          .map((story) => story?.agentTimings?.[agentKey]?.durationSeconds ?? story?.agentTimings?.[agentName]?.durationSeconds ?? null)
          .filter((value) => typeof value === 'number')
          .reduce((sum, value, _, arr) => sum + value / arr.length, 0) || 0,
    };
  });
}

function CurrentWorkItemBanner({ currentWorkItem }) {
  return (
    <div className="mb-6 overflow-hidden rounded-xl bg-gradient-to-r from-violet-600 via-violet-500 to-sky-500 p-[1px] shadow-lg shadow-violet-500/10">
      <div className="rounded-[11px] bg-white px-5 py-5">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-violet-500">Current Work Item</div>
            <h2 className="mt-2 text-2xl font-semibold text-gray-900">{currentWorkItem.title}</h2>
            <div className="mt-3 flex flex-wrap items-center gap-2 text-sm text-gray-500">
              <span className="rounded-full bg-gray-100 px-2.5 py-1 font-semibold text-gray-700">#{currentWorkItem.id}</span>
              {currentWorkItem.state ? (
                <span className="rounded-full bg-sky-100 px-2.5 py-1 font-semibold text-sky-700">{currentWorkItem.state}</span>
              ) : null}
              {currentWorkItem.autonomyLevel ? (
                <span className="rounded-full bg-violet-100 px-2.5 py-1 font-semibold text-violet-700">
                  Autonomy L{currentWorkItem.autonomyLevel}
                </span>
              ) : null}
            </div>
          </div>
          <div className="text-sm text-gray-500">
            <div>{currentWorkItem.elapsedTime ? `Elapsed ${currentWorkItem.elapsedTime}` : 'Currently active'}</div>
            <Link
              to={`/story/${currentWorkItem.id}`}
              state={{ story: currentWorkItem }}
              className="mt-3 inline-flex font-semibold text-violet-500 hover:text-violet-600"
            >
              View Details
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}

function FailedWorkItemBanner({ story, recentActivity }) {
  if (!story) {
    return null;
  }

  const latestTimestamp = resolveLatestTimestamp(story.workItemId, recentActivity);
  const latestFailureEntry = resolveLatestFailureEntry(story.workItemId, recentActivity);
  const failedAgent = Object.entries(story.agents ?? {}).find(([, status]) => status === 'failed')?.[0] ?? story.currentAgent ?? 'Pipeline';

  return (
    <div className="mb-6 overflow-hidden rounded-xl bg-gradient-to-r from-red-600 via-rose-500 to-orange-500 p-[1px] shadow-lg shadow-red-500/10">
      <div className="rounded-[11px] bg-white px-5 py-5">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-red-500">Latest Failure</div>
            <h2 className="mt-2 text-2xl font-semibold text-gray-900">{story.title}</h2>
            <div className="mt-3 flex flex-wrap items-center gap-2 text-sm text-gray-500">
              <span className="rounded-full bg-gray-100 px-2.5 py-1 font-semibold text-gray-700">#{story.workItemId}</span>
              <span className="rounded-full bg-red-100 px-2.5 py-1 font-semibold text-red-700">{story.workItemState ?? 'Agent Failed'}</span>
              <span className="rounded-full bg-amber-100 px-2.5 py-1 font-semibold text-amber-700">{failedAgent}</span>
            </div>
            {latestFailureEntry?.message ? (
              <p className="mt-3 max-w-3xl text-sm text-gray-600">{latestFailureEntry.message}</p>
            ) : null}
          </div>
          <div className="text-sm text-gray-500">
            <div>{latestTimestamp ? `Updated ${formatRelativeTime(latestTimestamp)}` : 'Failure detected'}</div>
            <Link
              to={`/story/${story.workItemId}`}
              state={{ story }}
              className="mt-3 inline-flex font-semibold text-red-500 hover:text-red-600"
            >
              View Failure
            </Link>
          </div>
        </div>
      </div>
    </div>
  );
}

function IdleBanner() {
  return (
    <div className="mb-6 rounded-xl border border-dashed border-gray-200 bg-white px-5 py-5 text-sm text-gray-500 shadow-xs">
      Pipeline idle
    </div>
  );
}

function AgentPipeline({ agents }) {
  const orderedAgents = AGENT_ORDER.map(
    (name) => agents.find((agent) => agent.name === name) ?? { name, status: 'idle', storiesProcessed: 0, avgDurationSeconds: 0 },
  );

  return (
    <div className="mb-6 rounded-xl bg-white p-5 shadow-xs">
      <div className="mb-5 flex items-center justify-between">
        <div>
          <h2 className="font-semibold text-gray-800">Agent Pipeline</h2>
          <p className="mt-1 text-sm text-gray-500">Planning through deployment, ordered left to right.</p>
        </div>
      </div>
      <div className="overflow-x-auto pb-2">
        <div className="flex min-w-max items-center gap-3">
          {orderedAgents.map((agent, index) => (
            <div key={agent.name} className="flex items-center gap-3">
              <AgentCard agent={agent} />
              {index < orderedAgents.length - 1 ? (
                <svg className="h-6 w-6 shrink-0 text-gray-300" viewBox="0 0 24 24" fill="none">
                  <path d="M8 5l8 7-8 7" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              ) : null}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function AgentDurationChart({ agents }) {
  const chartData = AGENT_ORDER.map((agentName) => {
    const agent = agents.find((entry) => entry.name === agentName);
    return {
      name: agentName.replace('Agent', ''),
      duration: agent?.avgDurationSeconds ?? 0,
    };
  });

  return (
    <div className="col-span-full rounded-xl bg-white shadow-xs xl:col-span-12">
      <header className="border-b border-gray-100 px-5 py-4">
        <h2 className="font-semibold text-gray-800">Average Agent Duration</h2>
      </header>
      <div className="h-[280px] px-3 py-4">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={chartData} margin={{ top: 12, right: 12, left: -20, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#e5e7eb" vertical={false} />
            <XAxis dataKey="name" tickLine={false} axisLine={false} tick={{ fill: '#6b7280', fontSize: 12 }} />
            <YAxis tickLine={false} axisLine={false} tick={{ fill: '#6b7280', fontSize: 12 }} />
            <RechartsTooltip formatter={(value) => formatDuration(Number(value))} />
            <Bar dataKey="duration" fill="#8470ff" radius={[8, 8, 0, 0]} />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

export default function Overview() {
  const { data, error, loading } = useOutletContext();

  if (loading && !data) {
    return <div className="rounded-xl bg-white px-5 py-8 text-sm text-gray-500 shadow-xs">Loading orchestration status...</div>;
  }

  if (error && !data) {
    return <div className="rounded-xl border border-red-200 bg-red-50 px-5 py-8 text-sm text-red-600 shadow-xs">{error}</div>;
  }

  const stats = data?.stats ?? {};
  const agents = (data?.agents?.length ? data.agents : buildAgentCardsFromStories(data?.stories, data?.recentActivity)).map((agent) => ({
    ...agent,
    status: normalizeAgentStatus(agent.status),
  }));
  const recentActivity = data?.recentActivity ?? [];
  const queuedTasks = data?.queuedTasks ?? [];
  const latestFailedStory = (data?.stories ?? [])
    .filter((story) => story.workItemState === 'Agent Failed' || Object.values(story.agents ?? {}).some((status) => status === 'failed'))
    .sort((left, right) => {
      const leftTs = resolveLatestTimestamp(left.workItemId, recentActivity);
      const rightTs = resolveLatestTimestamp(right.workItemId, recentActivity);
      return new Date(rightTs ?? 0).getTime() - new Date(leftTs ?? 0).getTime();
    })[0] ?? null;

  return (
    <>
      <div className="mb-8 grid gap-6 md:grid-cols-2 xl:grid-cols-5">
        <MetricCard label="Stories Processed (24h)" value={String(stats.storiesLast24h ?? 0)} detail="Completed in the last 24 hours." />
        <MetricCard label="Total Stories" value={String(stats.totalStoriesProcessed ?? 0)} detail="Lifetime throughput across the pipeline." />
        <MetricCard label="Avg Cycle Time" value={`${stats.avgCycleTimeMinutes ?? 0} min`} detail="Average end-to-end story processing time." />
        <MetricCard label="Success Rate" value={formatPercent(stats.successRate)} detail="Successful completions across processed work." />
        <MetricCard label="Hours Saved" value={String(stats.estimatedHoursSaved ?? 0)} detail="Estimated engineering time reclaimed." />
      </div>

      {data?.currentWorkItem ? <CurrentWorkItemBanner currentWorkItem={data.currentWorkItem} /> : null}
      {!data?.currentWorkItem && latestFailedStory ? <FailedWorkItemBanner story={latestFailedStory} recentActivity={recentActivity} /> : null}
      {!data?.currentWorkItem && !latestFailedStory ? <IdleBanner /> : null}
      <AgentPipeline agents={agents} />

      <div className="grid grid-cols-12 gap-6">
        <AgentDurationChart agents={agents} />
        <StoryQueueTable queue={queuedTasks} />
        <AgentActivityFeed entries={recentActivity} />
      </div>
    </>
  );
}
