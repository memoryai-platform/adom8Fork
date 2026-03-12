import { useCallback, useEffect, useRef, useState } from 'react';

import { config } from '../config';
import { getCurrentStatus } from '../api';

export function useAgentStatus(appKey, onUnauthorized) {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [lastUpdated, setLastUpdated] = useState(null);
  const [connectionStatus, setConnectionStatus] = useState('connecting');
  const failureCountRef = useRef(0);
  const intervalRef = useRef(null);
  const inFlightRef = useRef(false);

  const clearPolling = useCallback(() => {
    if (intervalRef.current) {
      window.clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  }, []);

  const fetchStatus = useCallback(async (isInitialLoad = false) => {
    if (!appKey || inFlightRef.current) {
      return;
    }

    inFlightRef.current = true;

    if (isInitialLoad) {
      setLoading(true);
    }

    try {
      const nextData = await getCurrentStatus(appKey);
      failureCountRef.current = 0;
      setData(nextData);
      setError('');
      setLastUpdated(new Date().toISOString());
      setConnectionStatus(document.visibilityState === 'hidden' ? 'paused' : 'live');
    } catch (requestError) {
      if (requestError.code === 401) {
        onUnauthorized?.();
        return;
      }

      failureCountRef.current += 1;
      setError(requestError.message || 'Failed to refresh status');
      setConnectionStatus(failureCountRef.current >= 3 ? 'disconnected' : 'reconnecting');
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
      setConnectionStatus('connecting');
      return undefined;
    }

    fetchStatus(true);

    const startPolling = () => {
      clearPolling();
      setConnectionStatus((current) => current === 'disconnected' ? current : 'live');
      intervalRef.current = window.setInterval(() => {
        fetchStatus(false);
      }, config.pollIntervalMs);
    };

    const handleVisibilityChange = () => {
      if (document.visibilityState === 'hidden') {
        clearPolling();
        setConnectionStatus((current) => current === 'disconnected' ? current : 'paused');
        return;
      }

      fetchStatus(false);
      startPolling();
    };

    if (document.visibilityState !== 'hidden') {
      startPolling();
    } else {
      setConnectionStatus('paused');
    }

    document.addEventListener('visibilitychange', handleVisibilityChange);

    return () => {
      document.removeEventListener('visibilitychange', handleVisibilityChange);
      clearPolling();
    };
  }, [appKey, clearPolling, fetchStatus]);

  return {
    connectionStatus,
    data,
    error,
    lastUpdated,
    loading,
  };
}
