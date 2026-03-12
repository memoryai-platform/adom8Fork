import { useCallback, useEffect, useState } from 'react';

import { APP_KEY_STORAGE_KEY } from '../constants';

export function useAppKey() {
  const [appKey, setAppKeyState] = useState('');
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const storedKey = localStorage.getItem(APP_KEY_STORAGE_KEY) || '';
    const url = new URL(window.location.href);
    const queryKey = url.searchParams.get('appKey') || '';
    const nextKey = queryKey || storedKey;

    if (queryKey) {
      localStorage.setItem(APP_KEY_STORAGE_KEY, queryKey);
      url.searchParams.delete('appKey');
      window.history.replaceState({}, '', `${url.pathname}${url.search}${url.hash}`);
    }

    setAppKeyState(nextKey);
    setReady(true);
  }, []);

  const setAppKey = useCallback((value) => {
    const normalizedValue = String(value || '').trim();

    if (!normalizedValue) {
      return;
    }

    localStorage.setItem(APP_KEY_STORAGE_KEY, normalizedValue);
    setAppKeyState(normalizedValue);
  }, []);

  const clearAppKey = useCallback(() => {
    localStorage.removeItem(APP_KEY_STORAGE_KEY);
    setAppKeyState('');
  }, []);

  return {
    appKey,
    clearAppKey,
    ready,
    setAppKey,
  };
}
