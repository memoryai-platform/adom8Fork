import { AGENT_ORDER } from '../constants';

export function getPhaseStatus(story, agentName) {
  if (!story) {
    return 'pending';
  }

  const currentIndex = AGENT_ORDER.indexOf(story.currentAgent);

  if (currentIndex === -1) {
    return 'pending';
  }

  const agentIndex = AGENT_ORDER.indexOf(agentName);

  if (agentIndex < currentIndex) {
    return 'completed';
  }

  if (agentIndex === currentIndex) {
    return 'active';
  }

  return 'pending';
}

export function buildFallbackStoryDetail(storyId, statusData, seedStory) {
  const numericId = Number(storyId);
  const currentItem = statusData?.currentWorkItem?.id === numericId ? statusData.currentWorkItem : null;
  const queuedItem = statusData?.queuedTasks?.find((item) => item.workItemId === numericId) ?? null;
  const story = seedStory ?? currentItem ?? queuedItem;
  const activity = (statusData?.recentActivity ?? []).filter((entry) => entry.workItemId === numericId);
  const updatedDate = activity[0]?.timestamp ?? null;
  const timelineSource = seedStory?.currentAgent
    ? seedStory
    : currentItem?.state
      ? { currentAgent: currentItem.state.replace(/\s+Agent$/i, '') }
      : null;

  if (!story) {
    return null;
  }

  return {
    id: numericId,
    title: story.title,
    state: story.state ?? story.workItemState ?? 'AI Agent',
    autonomyLevel: story.autonomyLevel ?? currentItem?.autonomyLevel ?? null,
    currentAgent: story.currentAgent ?? currentItem?.state ?? null,
    lastAgent: currentItem?.lastAgent ?? null,
    createdDate: currentItem?.createdDate ?? null,
    updatedDate,
    phases: AGENT_ORDER.map((agentName) => ({
      agent: agentName,
      status: getPhaseStatus(timelineSource, agentName),
      completedAt: getPhaseStatus(timelineSource, agentName) === 'completed' ? updatedDate : null,
    })),
    activity,
  };
}
