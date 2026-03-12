import { useEffect, useMemo, useRef } from 'react';
import { Link } from 'react-router-dom';

import { formatRelativeTime } from '../utils/formatting';

/**
 * @param {{entries: Array<{timestamp: string, agent: string, workItemId: number, message: string}>}} props
 */
export default function AgentActivityFeed({ entries }) {
  const containerRef = useRef(null);
  const items = useMemo(
    () =>
      [...(entries ?? [])]
        .sort((left, right) => new Date(left.timestamp).getTime() - new Date(right.timestamp).getTime())
        .slice(-20),
    [entries],
  );

  useEffect(() => {
    if (!containerRef.current) {
      return;
    }

    containerRef.current.scrollTop = containerRef.current.scrollHeight;
  }, [items]);

  return (
    <div className="col-span-full xl:col-span-5 rounded-xl bg-white shadow-xs">
      <header className="border-b border-gray-100 px-5 py-4">
        <h2 className="font-semibold text-gray-800">Activity Feed</h2>
      </header>
      <div ref={containerRef} className="max-h-[420px] space-y-1 overflow-y-auto p-3">
        {items.length ? (
          items.map((entry, index) => (
            <div key={`${entry.timestamp}-${entry.agent}-${index}`} className="flex rounded-lg px-2 py-3 hover:bg-gray-50">
              <div className="mr-3 mt-1 h-9 w-9 shrink-0 rounded-full bg-violet-500 text-white">
                <div className="flex h-full items-center justify-center text-xs font-semibold">
                  {entry.agent.replace('Agent', '').slice(0, 2).toUpperCase()}
                </div>
              </div>
              <div className="min-w-0 grow border-b border-gray-100 pb-3 text-sm last:border-b-0">
                <div className="flex items-center justify-between gap-3">
                  <div className="truncate font-medium text-gray-900">{entry.message}</div>
                  <div className="shrink-0 text-xs text-gray-400">{formatRelativeTime(entry.timestamp)}</div>
                </div>
                <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-gray-500">
                  <span className="inline-flex rounded-full bg-sky-100 px-2 py-1 font-semibold text-sky-700">{entry.agent}</span>
                  <span>Story</span>
                  <Link to={`/story/${entry.workItemId}`} className="font-semibold text-violet-500 hover:text-violet-600">
                    #{entry.workItemId}
                  </Link>
                </div>
              </div>
            </div>
          ))
        ) : (
          <div className="rounded-lg border border-dashed border-gray-200 px-4 py-8 text-center text-sm text-gray-500">
            No recent activity yet.
          </div>
        )}
      </div>
    </div>
  );
}
