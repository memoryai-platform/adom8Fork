import { Link, useOutletContext } from 'react-router-dom';

import { formatRelativeTime, formatTimestamp } from '../utils/formatting';
import { formatAgentLabel, normalizeAgentKey } from '../utils/story';

function getLatestStoryTimestamp(story, recentActivity) {
  const activityTimestamp = (recentActivity ?? [])
    .filter((entry) => entry.workItemId === story.workItemId && entry.timestamp)
    .sort((left, right) => new Date(right.timestamp).getTime() - new Date(left.timestamp).getTime())[0]?.timestamp ?? null;

  if (activityTimestamp) {
    return activityTimestamp;
  }

  const timingTimestamp = Object.values(story?.agentTimings ?? {})
    .flatMap((timing) => [timing?.completedAt, timing?.startedAt])
    .filter(Boolean)
    .sort((left, right) => new Date(right).getTime() - new Date(left).getTime())[0] ?? null;

  return timingTimestamp;
}

function getStoryStatus(story) {
  const statuses = Object.values(story?.agents ?? {}).map((value) => String(value ?? '').toLowerCase());

  if (statuses.some((status) => status === 'failed' || status === 'needs_revision')) {
    return 'failed';
  }

  if (statuses.length && statuses.every((status) => status === 'completed' || status === 'skipped')) {
    return 'completed';
  }

  if (statuses.some((status) => status === 'in_progress' || status === 'awaiting_code')) {
    return 'active';
  }

  return 'pending';
}

function resolveStageLabel(story) {
  const explicit = formatAgentLabel(story?.currentAiAgent ?? story?.currentAgent);
  if (explicit) {
    return explicit;
  }

  const activeEntry = Object.entries(story?.agents ?? {}).find(([, status]) =>
    ['in_progress', 'awaiting_code'].includes(String(status ?? '').toLowerCase()),
  );

  if (activeEntry) {
    return formatAgentLabel(activeEntry[0]);
  }

  const completedEntry = [...Object.entries(story?.agents ?? {})].reverse().find(([, status]) =>
    ['completed', 'skipped'].includes(String(status ?? '').toLowerCase()),
  );

  return completedEntry ? formatAgentLabel(completedEntry[0]) : 'Queued';
}

function getTone(status) {
  switch (status) {
    case 'active':
      return {
        card: 'border-sky-200 bg-sky-50/70',
        pill: 'bg-sky-100 text-sky-700',
        dot: 'bg-sky-500',
        label: 'Active',
      };
    case 'completed':
      return {
        card: 'border-emerald-200 bg-emerald-50/70',
        pill: 'bg-emerald-100 text-emerald-700',
        dot: 'bg-emerald-500',
        label: 'Completed',
      };
    case 'failed':
      return {
        card: 'border-rose-200 bg-rose-50/70',
        pill: 'bg-rose-100 text-rose-700',
        dot: 'bg-rose-500',
        label: 'Failed',
      };
    default:
      return {
        card: 'border-gray-200 bg-gray-50/70',
        pill: 'bg-gray-100 text-gray-700',
        dot: 'bg-gray-400',
        label: 'Pending',
      };
  }
}

function StoryCard({ story, recentActivity }) {
  const status = getStoryStatus(story);
  const tone = getTone(status);
  const latestTimestamp = getLatestStoryTimestamp(story, recentActivity);
  const stageLabel = resolveStageLabel(story);

  return (
    <Link
      to={`/story/${story.workItemId}`}
      state={{ story }}
      className={`group rounded-2xl border p-5 transition hover:-translate-y-0.5 hover:shadow-md ${tone.card}`}
    >
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="text-xs font-semibold uppercase tracking-[0.18em] text-gray-400">Work Item #{story.workItemId}</div>
          <h2 className="mt-2 text-lg font-semibold text-gray-900 group-hover:text-ado-700">{story.title}</h2>
        </div>
        <span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${tone.pill}`}>{tone.label}</span>
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-2 text-xs">
        <span className="inline-flex items-center gap-2 rounded-full bg-white/80 px-2.5 py-1 font-semibold text-gray-700">
          <span className={`h-2 w-2 rounded-full ${tone.dot}`} />
          {stageLabel}
        </span>
        {story.workItemState ? (
          <span className="inline-flex rounded-full bg-white/80 px-2.5 py-1 font-semibold text-gray-700">
            {story.workItemState}
          </span>
        ) : null}
        {story.autonomyLevel ? (
          <span className="inline-flex rounded-full bg-ado-100 px-2.5 py-1 font-semibold text-ado-700">
            Autonomy L{story.autonomyLevel}
          </span>
        ) : null}
      </div>

      <div className="mt-4 grid grid-cols-2 gap-3 text-sm text-gray-600">
        <div className="rounded-xl bg-white/80 px-3 py-2">
          <div className="text-[11px] font-semibold uppercase tracking-[0.16em] text-gray-400">Updated</div>
          <div className="mt-1 font-medium text-gray-800">{latestTimestamp ? formatRelativeTime(latestTimestamp) : 'No activity yet'}</div>
        </div>
        <div className="rounded-xl bg-white/80 px-3 py-2">
          <div className="text-[11px] font-semibold uppercase tracking-[0.16em] text-gray-400">Completed Stages</div>
          <div className="mt-1 font-medium text-gray-800">
            {Object.values(story?.agents ?? {}).filter((value) => ['completed', 'skipped'].includes(String(value ?? '').toLowerCase())).length}
            /6
          </div>
        </div>
      </div>
    </Link>
  );
}

function StoryTable({ title, stories, recentActivity, emptyMessage }) {
  return (
    <section className="rounded-2xl bg-white shadow-xs">
      <header className="border-b border-gray-100 px-5 py-4">
        <h2 className="font-semibold text-gray-800">{title}</h2>
      </header>
      <div className="p-3">
        {stories.length ? (
          <div className="overflow-x-auto">
            <table className="w-full table-auto">
              <thead className="bg-gray-50 text-left text-xs uppercase text-gray-400">
                <tr>
                  <th className="p-2 font-semibold">Story</th>
                  <th className="p-2 font-semibold">Stage</th>
                  <th className="p-2 font-semibold">State</th>
                  <th className="p-2 font-semibold">Updated</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 text-sm">
                {stories.map((story) => {
                  const latestTimestamp = getLatestStoryTimestamp(story, recentActivity);

                  return (
                    <tr key={story.workItemId}>
                      <td className="p-2">
                        <Link
                          to={`/story/${story.workItemId}`}
                          state={{ story }}
                          className="font-semibold text-ado-500 hover:text-ado-600"
                        >
                          #{story.workItemId}
                        </Link>
                        <div className="mt-1 max-w-[34rem] truncate text-gray-700">{story.title}</div>
                      </td>
                      <td className="p-2 text-gray-700">{resolveStageLabel(story)}</td>
                      <td className="p-2 text-gray-700">{story.workItemState ?? getTone(getStoryStatus(story)).label}</td>
                      <td className="p-2 text-gray-500" title={latestTimestamp ? formatTimestamp(latestTimestamp) : undefined}>
                        {latestTimestamp ? formatRelativeTime(latestTimestamp) : 'N/A'}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="rounded-xl border border-dashed border-gray-200 px-4 py-8 text-center text-sm text-gray-500">
            {emptyMessage}
          </div>
        )}
      </div>
    </section>
  );
}

export default function Stories() {
  const { data, error, loading } = useOutletContext();

  if (loading && !data) {
    return <div className="rounded-xl bg-white px-5 py-8 text-sm text-gray-500 shadow-xs">Loading workstreams...</div>;
  }

  if (error && !data) {
    return <div className="rounded-xl border border-red-200 bg-red-50 px-5 py-8 text-sm text-red-600 shadow-xs">{error}</div>;
  }

  const stories = [...(data?.stories ?? [])];
  const recentActivity = data?.recentActivity ?? [];

  const sortedStories = stories.sort((left, right) => {
    const leftTs = getLatestStoryTimestamp(left, recentActivity);
    const rightTs = getLatestStoryTimestamp(right, recentActivity);
    return new Date(rightTs ?? 0).getTime() - new Date(leftTs ?? 0).getTime();
  });

  const activeStories = sortedStories.filter((story) => getStoryStatus(story) === 'active');
  const failedStories = sortedStories.filter((story) => getStoryStatus(story) === 'failed');
  const completedStories = sortedStories.filter((story) => getStoryStatus(story) === 'completed');

  return (
    <div className="space-y-6">
      <section className="rounded-2xl bg-gradient-to-r from-slate-900 via-slate-800 to-sky-700 px-6 py-6 text-white shadow-lg shadow-slate-900/10">
        <div className="flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
          <div>
            <div className="text-xs font-semibold uppercase tracking-[0.2em] text-sky-200">Parallel Workstreams</div>
            <h1 className="mt-2 text-3xl font-semibold">See every story moving through the pipeline</h1>
            <p className="mt-2 max-w-3xl text-sm text-slate-200">
              This view surfaces active, failed, and recently completed stories side by side so concurrent work does not disappear behind the current work item banner.
            </p>
          </div>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <div className="rounded-2xl bg-white/10 px-4 py-3 backdrop-blur">
              <div className="text-xs uppercase tracking-[0.16em] text-slate-300">Active</div>
              <div className="mt-2 text-2xl font-semibold">{activeStories.length}</div>
            </div>
            <div className="rounded-2xl bg-white/10 px-4 py-3 backdrop-blur">
              <div className="text-xs uppercase tracking-[0.16em] text-slate-300">Failed</div>
              <div className="mt-2 text-2xl font-semibold">{failedStories.length}</div>
            </div>
            <div className="rounded-2xl bg-white/10 px-4 py-3 backdrop-blur">
              <div className="text-xs uppercase tracking-[0.16em] text-slate-300">Completed</div>
              <div className="mt-2 text-2xl font-semibold">{completedStories.length}</div>
            </div>
            <div className="rounded-2xl bg-white/10 px-4 py-3 backdrop-blur">
              <div className="text-xs uppercase tracking-[0.16em] text-slate-300">Queued</div>
              <div className="mt-2 text-2xl font-semibold">{data?.queuedTasks?.length ?? 0}</div>
            </div>
          </div>
        </div>
      </section>

      <section>
        <div className="mb-4 flex items-center justify-between">
          <div>
            <h2 className="text-lg font-semibold text-gray-900">Active Stories</h2>
            <p className="mt-1 text-sm text-gray-500">Stories currently moving through Planning, Coding, Testing, Review, Documentation, or Deployment.</p>
          </div>
        </div>
        {activeStories.length ? (
          <div className="grid gap-4 xl:grid-cols-2 2xl:grid-cols-3">
            {activeStories.map((story) => (
              <StoryCard key={story.workItemId} story={story} recentActivity={recentActivity} />
            ))}
          </div>
        ) : (
          <div className="rounded-2xl border border-dashed border-gray-200 bg-white px-4 py-10 text-center text-sm text-gray-500 shadow-xs">
            No active stories right now.
          </div>
        )}
      </section>

      <div className="grid gap-6 xl:grid-cols-2">
        <StoryTable
          title="Needs Attention"
          stories={failedStories}
          recentActivity={recentActivity}
          emptyMessage="No failed stories right now."
        />
        <StoryTable
          title="Recently Completed"
          stories={completedStories.slice(0, 12)}
          recentActivity={recentActivity}
          emptyMessage="No completed stories yet."
        />
      </div>
    </div>
  );
}
