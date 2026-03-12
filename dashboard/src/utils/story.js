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
  const queuedItem = statusData?.queue?.find((item) => item.id === numericId) ?? null;
  const story = seedStory ?? currentItem ?? queuedItem;

  if (!story) {
    return null;
  }

  return {
    id: numericId,
    title: story.title,
    state: story.state ?? 'AI Agent',
    autonomyLevel: story.autonomyLevel,
    currentAgent: currentItem?.currentAgent ?? null,
    lastAgent: currentItem?.lastAgent ?? null,
    createdDate: currentItem?.createdDate ?? null,
    updatedDate: currentItem?.updatedDate ?? null,
    phases: AGENT_ORDER.map((agentName) => ({
      agent: agentName,
      status: getPhaseStatus(currentItem, agentName),
      completedAt: getPhaseStatus(currentItem, agentName) === 'completed' ? currentItem?.updatedDate ?? null : null,
    })),
    activity: (statusData?.activityFeed ?? []).filter((entry) => entry.workItemId === numericId),
  };
}
