export const config = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL || '',
  pollIntervalMs: 2000,
  healthPollIntervalMs: 60000,
  codebasePollIntervalMs: 60000,
  useMockData: import.meta.env.VITE_USE_MOCK_DATA === 'true',
};
