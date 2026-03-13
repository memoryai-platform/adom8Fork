import { AGENT_ORDER } from '../constants';

function stripAgentSuffix(value) {
  return String(value ?? '').replace(/\s*Agent$/i, '').trim();
}

function normalizeAgentKey(value) {
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

function formatAgentLabel(value) {
  const key = normalizeAgentKey(value);
  return key ? `${key} Agent` : value ?? null;
}

function mapAgentStatus(rawStatus) {
  switch (rawStatus) {
    case 'completed':
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
    return mapAgentStatus(rawStatus);
  }

  const normalizedCurrent = normalizeAgentKey(story?.currentAgent ?? story?.currentAiAgent ?? story?.state);
  if (!normalizedCurrent) {
    return 'pending';
  }

  const currentIndex = AGENT_ORDER.findIndex((entry) => stripAgentSuffix(entry) === normalizedCurrent);
  const agentIndex = AGENT_ORDER.findIndex((entry) => entry === agentName);

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

export function buildFallbackStoryDetail(storyId, statusData, seedStory) {
  const numericId = Number(storyId);
  const liveStory = statusData?.stories?.find((item) => item.workItemId === numericId) ?? null;
  const currentItem = statusData?.currentWorkItem?.id === numericId ? statusData.currentWorkItem : null;
  const queuedItem = statusData?.queuedTasks?.find((item) => item.workItemId === numericId) ?? null;
  const story = seedStory ?? liveStory ?? currentItem ?? queuedItem;
  const activity = (statusData?.recentActivity ?? []).filter((entry) => entry.workItemId === numericId);
  const updatedDate = activity[0]?.timestamp ?? null;
  const agentDetails = story?.agentDetails ?? liveStory?.agentDetails ?? {};
  const codingDetails = agentDetails?.Coding?.additionalData ?? agentDetails?.CodingAgent?.additionalData ?? null;
  const githubIssueNumber = Number(codingDetails?.issueNumber ?? 0) || null;
  const githubIssueUrl = githubIssueNumber && statusData?.githubOwner && statusData?.githubRepo
    ? `https://github.com/${statusData.githubOwner}/${statusData.githubRepo}/issues/${githubIssueNumber}`
    : null;

  if (!story) {
    return null;
  }

  return {
    id: numericId,
    title: story.title,
    state: story.state ?? story.workItemState ?? 'AI Agent',
    autonomyLevel: story.autonomyLevel ?? currentItem?.autonomyLevel ?? null,
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
    })),
    activity,
  };
}
