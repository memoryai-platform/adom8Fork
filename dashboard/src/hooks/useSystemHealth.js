import { useCallback, useEffect, useRef, useState } from 'react';

import { getSystemHealth } from '../api';
import { config } from '../config';

export function useSystemHealth(appKey, onUnauthorized) {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [lastUpdated, setLastUpdated] = useState(null);
  const intervalRef = useRef(null);
  const inFlightRef = useRef(false);

  const clearPolling = useCallback(() => {
    if (intervalRef.current) {
      window.clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  }, []);

  const refresh = useCallback(async (isInitialLoad = false) => {
    if (!appKey || inFlightRef.current) {
      return;
    }

    inFlightRef.current = true;

    if (isInitialLoad) {
      setLoading(true);
    }

    try {
      const nextData = await getSystemHealth(appKey);
      setData(nextData);
      setError('');
      setLastUpdated(new Date().toISOString());
    } catch (requestError) {
      if (requestError.code === 401 || requestError.code === 403) {
        onUnauthorized?.();
        return;
      }

      setError(requestError.message || 'Failed to refresh system health');
    } finally {
      inFlightRef.current = false;
      if (isInitialLoad) {
        setLoading(false);
      }
    }
  }, [appKey, onUnauthorized]);

  useEffect(() => {
    if (!appKey) {
      clearPolling();
      setData(null);
      setLoading(false);
      setError('');
      setLastUpdated(null);
      return undefined;
    }

    refresh(true);

    const startPolling = () => {
      clearPolling();
      intervalRef.current = window.setInterval(() => {
        refresh(false);
      }, config.healthPollIntervalMs);
    };

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'hidden') {
        clearPolling();
        return;
      }

      refresh(false);
      startPolling();
    };

    if (document.visibilityState !== 'hidden') {
      startPolling();
    }

    document.addEventListener('visibilitychange', handleVisibilityChange);

    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      clearPolling();
    };
  }, [appKey, clearPolling, refresh]);

  return {
    data,
    error,
    lastUpdated,
    loading,
    refresh: () => refresh(false),
  };
}
