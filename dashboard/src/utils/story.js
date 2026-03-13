import { AGENT_ORDER } from '../constants';

export function stripAgentSuffix(value) {
  return String(value ?? '').replace(/\s*Agent$/i, '').trim();
}

export function normalizeAgentKey(value) {
  const normalized = stripAgentSuffix(value).toLowerCase();
  switch (normalized) {
    case 'planning':
    case 'planningagent':
      return 'Planning';
    case 'coding':
    case 'codingagent':
      return 'Coding';
    case 'testing':
    case 'testingagent':
      return 'Testing';
    case 'review':
    case 'reviewagent':
      return 'Review';
    case 'documentation':
    case 'documentationagent':
      return 'Documentation';
    case 'deployment':
    case 'deploy':
    case 'deploymentagent':
    case 'deployagent':
      return 'Deployment';
    default:
      return null;
  }
}

export function formatAgentLabel(value) {
  const key = normalizeAgentKey(value);
  return key ? `${key} Agent` : value ?? null;
}

function getAgentOrderIndex(agentName) {
  return AGENT_ORDER.findIndex((entry) => stripAgentSuffix(entry) === agentName);
}

function resolveCurrentAgentSignal(liveStory, currentItem, seedStory) {
  return liveStory?.currentAiAgent
    ?? liveStory?.currentAgent
    ?? currentItem?.state
    ?? seedStory?.currentAiAgent
    ?? seedStory?.currentAgent
    ?? seedStory?.state
    ?? null;
}

function mapAgentStatus(rawStatus) {
  switch (rawStatus) {
    case 'completed':
    case 'skipped':
      return 'completed';
    case 'failed':
      return 'failed';
    case 'in_progress':
    case 'awaiting_code':
      return 'active';
    case 'needs_revision':
      return 'failed';
    default:
      return 'pending';
  }
}

function getAgentTimelineStatus(story, agentName) {
  const agentKey = stripAgentSuffix(agentName);
  const rawStatus = story?.agents?.[agentKey] ?? story?.agents?.[agentName] ?? null;
  if (rawStatus) {
    const mapped = mapAgentStatus(rawStatus);
    if (mapped === 'active' || mapped === 'failed' || mapped === 'completed') {
      return mapped;
    }
  }

  const normalizedCurrent = normalizeAgentKey(story?.currentAiAgent ?? story?.currentAgent);
  const currentIndex = normalizedCurrent ? getAgentOrderIndex(normalizedCurrent) : -1;
  const agentIndex = getAgentOrderIndex(agentKey);

  if (currentIndex === -1 || agentIndex === -1) {
    return 'pending';
  }

  if (agentIndex < currentIndex) {
    return 'completed';
  }

  if (agentIndex === currentIndex) {
    return 'active';
  }

  return 'pending';
}

export function getPhaseStatus(story, agentName) {
  if (!story) {
    return 'pending';
  }

  return getAgentTimelineStatus(story, agentName);
}

function getLatestTimestamp(activity) {
  return (activity ?? []).reduce((latest, entry) => {
    if (!entry?.timestamp) {
      return latest;
    }

    if (!latest) {
      return entry.timestamp;
    }

    return new Date(entry.timestamp).getTime() > new Date(latest).getTime()
      ? entry.timestamp
      : latest;
  }, null);
}

function mergeAgentData(...sources) {
  return sources.reduce((accumulator, source) => {
    if (!source) {
      return accumulator;
    }

    return { ...accumulator, ...source };
  }, {});
}

function buildStorySnapshot({ liveStory, currentItem, queuedItem, seedStory }) {
  const resolvedCurrentAgent = resolveCurrentAgentSignal(liveStory, currentItem, seedStory);
  const resolvedWorkItemState = liveStory?.workItemState ?? seedStory?.workItemState ?? null;

  return {
    ...(queuedItem ?? {}),
    ...(seedStory ?? {}),
    ...(liveStory ?? {}),
    title: liveStory?.title ?? seedStory?.title ?? currentItem?.title ?? queuedItem?.title ?? null,
    currentAgent: resolvedCurrentAgent,
    currentAiAgent: resolvedCurrentAgent,
    workItemState: resolvedWorkItemState,
    autonomyLevel: currentItem?.autonomyLevel ?? liveStory?.autonomyLevel ?? seedStory?.autonomyLevel ?? queuedItem?.autonomyLevel ?? null,
    agents: liveStory?.agents ?? seedStory?.agents ?? queuedItem?.agents ?? {},
    agentDetails: mergeAgentData(queuedItem?.agentDetails, seedStory?.agentDetails, liveStory?.agentDetails),
    agentTimings: mergeAgentData(queuedItem?.agentTimings, seedStory?.agentTimings, liveStory?.agentTimings),
  };
}

export function buildFallbackStoryDetail(storyId, statusData, seedStory) {
  const numericId = Number(storyId);
  const liveStory = statusData?.stories?.find((item) => item.workItemId === numericId) ?? null;
  const currentItem = statusData?.currentWorkItem?.id === numericId ? statusData.currentWorkItem : null;
  const queuedItem = statusData?.queuedTasks?.find((item) => item.workItemId === numericId) ?? null;
  const hasStoryData = Boolean(seedStory || liveStory || currentItem || queuedItem);

  if (!hasStoryData) {
    return null;
  }

  const story = buildStorySnapshot({ liveStory, currentItem, queuedItem, seedStory });
  const activity = (statusData?.recentActivity ?? []).filter((entry) => entry.workItemId === numericId);
  const updatedDate = getLatestTimestamp(activity);
  const codingDetails = story.agentDetails?.Coding?.additionalData ?? story.agentDetails?.CodingAgent?.additionalData ?? null;
  const githubIssueNumber = Number(codingDetails?.issueNumber ?? 0) || null;
  const githubIssueUrl = githubIssueNumber && statusData?.githubOwner && statusData?.githubRepo
    ? `https://github.com/${statusData.githubOwner}/${statusData.githubRepo}/issues/${githubIssueNumber}`
    : null;

  return {
    id: numericId,
    title: story.title,
    state: story.workItemState ?? formatAgentLabel(story.currentAiAgent ?? story.currentAgent) ?? 'AI Agent',
    workItemState: story.workItemState,
    autonomyLevel: story.autonomyLevel,
    currentAgent: formatAgentLabel(story.currentAiAgent ?? story.currentAgent ?? currentItem?.state),
    lastAgent: currentItem?.lastAgent ?? null,
    createdDate: currentItem?.createdDate ?? null,
    updatedDate,
    githubIssueNumber,
    githubIssueUrl,
    githubDelegated: codingDetails?.mode === 'copilot-delegated',
    delegatedAgent: codingDetails?.agent ?? null,
    phases: AGENT_ORDER.map((agentName) => ({
      agent: agentName,
      status: getPhaseStatus(story, agentName),
      completedAt:
        (story?.agentTimings?.[stripAgentSuffix(agentName)]?.completedAt ?? story?.agentTimings?.[agentName]?.completedAt ?? null)
        || (getPhaseStatus(story, agentName) === 'completed' ? updatedDate : null),
      details: story?.agentDetails?.[stripAgentSuffix(agentName)] ?? story?.agentDetails?.[agentName] ?? null,
      rawStatus: story?.agents?.[stripAgentSuffix(agentName)] ?? story?.agents?.[agentName] ?? null,
    })),
    activity,
  };
}
