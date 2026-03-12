import { AGENT_ORDER } from './constants';

export const mockStatusPayload = {
  currentWorkItem: {
    id: 1042,
    title: 'Implement JWT refresh token rotation',
    state: 'AI Agent',
    autonomyLevel: 3,
    currentAgent: 'CodingAgent',
    lastAgent: 'PlanningAgent',
    createdDate: '2026-03-10T14:23:00Z',
    updatedDate: '2026-03-11T09:15:00Z',
  },
  agents: [
    { name: 'PlanningAgent', status: 'completed', lastRun: '2026-03-11T09:10:00Z', storiesProcessed: 47, avgDurationSeconds: 38 },
    { name: 'CodingAgent', status: 'active', lastRun: '2026-03-11T09:15:00Z', storiesProcessed: 44, avgDurationSeconds: 142 },
    { name: 'TestingAgent', status: 'idle', lastRun: '2026-03-11T08:50:00Z', storiesProcessed: 43, avgDurationSeconds: 67 },
    { name: 'ReviewAgent', status: 'idle', lastRun: '2026-03-11T08:52:00Z', storiesProcessed: 43, avgDurationSeconds: 29 },
    { name: 'DocumentationAgent', status: 'idle', lastRun: '2026-03-11T08:53:00Z', storiesProcessed: 41, avgDurationSeconds: 22 },
    { name: 'DeploymentAgent', status: 'idle', lastRun: '2026-03-10T16:30:00Z', storiesProcessed: 12, avgDurationSeconds: 95 },
  ],
  queue: [
    { id: 1043, title: 'Fix null reference in payment service', autonomyLevel: 1, queuePosition: 1 },
    { id: 1044, title: 'Add pagination to reports grid', autonomyLevel: 3, queuePosition: 2 },
  ],
  stats: {
    totalStoriesProcessed: 312,
    storiesLast24h: 14,
    avgCycleTimeMinutes: 4.2,
    successRate: 0.94,
    estimatedHoursSaved: 186,
  },
  activityFeed: [
    { timestamp: '2026-03-11T09:15:00Z', agent: 'CodingAgent', workItemId: 1042, message: 'Generated 3 files, opened PR #87' },
    { timestamp: '2026-03-11T09:10:00Z', agent: 'PlanningAgent', workItemId: 1042, message: 'Plan approved, PLAN.md written to branch' },
    { timestamp: '2026-03-11T08:53:00Z', agent: 'DocumentationAgent', workItemId: 1041, message: 'Release notes generated' },
    { timestamp: '2026-03-11T08:52:00Z', agent: 'ReviewAgent', workItemId: 1041, message: 'Review score 91/100 approved' },
    { timestamp: '2026-03-11T08:50:00Z', agent: 'TestingAgent', workItemId: 1041, message: '12 tests generated, all passing' },
  ],
};

export function getMockStatusPayload() {
  return JSON.parse(JSON.stringify(mockStatusPayload));
}

function buildPhases(currentAgentName) {
  const currentIndex = AGENT_ORDER.indexOf(currentAgentName);

  return AGENT_ORDER.map((agentName, index) => ({
    agent: agentName,
    status: currentIndex === -1 ? 'pending' : index < currentIndex ? 'completed' : index === currentIndex ? 'active' : 'pending',
    completedAt: index < currentIndex ? mockStatusPayload.currentWorkItem.updatedDate : null,
  }));
}

export function getMockStoryDetail(storyId) {
  const normalizedId = Number(storyId);
  const current = mockStatusPayload.currentWorkItem?.id === normalizedId ? mockStatusPayload.currentWorkItem : null;
  const queued = mockStatusPayload.queue.find((item) => item.id === normalizedId) ?? null;
  const base = current ?? queued;

  if (!base) {
    return null;
  }

  return {
    id: normalizedId,
    title: base.title,
    state: base.state ?? 'AI Agent',
    autonomyLevel: base.autonomyLevel,
    currentAgent: current?.currentAgent ?? null,
    lastAgent: current?.lastAgent ?? null,
    createdDate: current?.createdDate ?? '2026-03-10T14:23:00Z',
    updatedDate: current?.updatedDate ?? '2026-03-11T09:15:00Z',
    phases: current ? buildPhases(current.currentAgent) : AGENT_ORDER.map((agentName) => ({
      agent: agentName,
      status: 'pending',
      completedAt: null,
    })),
    activity: mockStatusPayload.activityFeed.filter((entry) => entry.workItemId === normalizedId),
  };
}
