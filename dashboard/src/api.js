import { config } from './config';
import { getMockStatusPayload, getMockStoryDetail } from './mockData';

const STATUS_ENDPOINTS = ['/api/GetCurrentStatus', '/api/status'];
const STORY_DETAIL_ENDPOINTS = ['/api/GetStoryDetail'];

function getBaseOrigin() {
  if (config.apiBaseUrl) {
    return config.apiBaseUrl;
  }

  return window.location.origin;
}

function appendAuthParams(url, appKey) {
  if (!appKey) {
    return url;
  }

  url.searchParams.set('appKey', appKey);
  url.searchParams.set('code', appKey);
  return url;
}

function buildUrl(path, appKey, query = {}) {
  const url = new URL(path, getBaseOrigin());
  Object.entries(query).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      url.searchParams.set(key, String(value));
    }
  });

  return appendAuthParams(url, appKey);
}

async function fetchJsonFromCandidates(paths, appKey, query) {
  let lastError = null;

  for (const path of paths) {
    const response = await fetch(buildUrl(path, appKey, query), {
      headers: {
        Accept: 'application/json',
      },
      cache: 'no-store',
    });

    if (response.status === 401) {
      const unauthorizedError = new Error('Unauthorized');
      unauthorizedError.code = 401;
      throw unauthorizedError;
    }

    if (response.status === 404) {
      lastError = new Error('Not Found');
      lastError.code = 404;
      continue;
    }

    if (!response.ok) {
      const requestError = new Error(`Request failed with status ${response.status}`);
      requestError.code = response.status;
      throw requestError;
    }

    return response.json();
  }

  throw lastError ?? new Error('Request failed');
}

export async function validateAppKey(appKey) {
  const trimmed = String(appKey || '').trim();
  if (!trimmed) {
    return false;
  }

  if (config.useMockData) {
    return true;
  }

  try {
    await fetchJsonFromCandidates(STATUS_ENDPOINTS, trimmed);
    return true;
  } catch (error) {
    if (error.code === 401 || error.code === 404) {
      return false;
    }

    throw error;
  }
}

export async function getCurrentStatus(appKey) {
  if (config.useMockData) {
    return getMockStatusPayload();
  }

  return fetchJsonFromCandidates(STATUS_ENDPOINTS, appKey);
}

export async function getStoryDetail(storyId, appKey) {
  if (config.useMockData) {
    return getMockStoryDetail(storyId);
  }

  return fetchJsonFromCandidates(STORY_DETAIL_ENDPOINTS, appKey, { id: storyId });
}
