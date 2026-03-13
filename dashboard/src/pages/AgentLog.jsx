import { useEffect, useMemo, useState } from 'react';
import { Link, useOutletContext } from 'react-router-dom';

import { formatTimestamp } from '../utils/formatting';

const PAGE_SIZE = 10;

export default function AgentLog() {
  const { data } = useOutletContext();
  const [selectedAgent, setSelectedAgent] = useState('all');
  const [page, setPage] = useState(1);
  const entries = data?.recentActivity ?? [];

  const agentNames = useMemo(() => ['all', ...new Set(entries.map((entry) => entry.agent))], [entries]);
  const filteredEntries = useMemo(
    () =>
      entries.filter((entry) => selectedAgent === 'all' || entry.agent === selectedAgent),
    [entries, selectedAgent],
  );
  const totalPages = Math.max(1, Math.ceil(filteredEntries.length / PAGE_SIZE));
  const pageEntries = filteredEntries.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  useEffect(() => {
    if (page > totalPages) {
      setPage(totalPages);
    }
  }, [page, totalPages]);

  const handleFilterChange = (event) => {
    setSelectedAgent(event.target.value);
    setPage(1);
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-2xl font-semibold text-gray-900">Agent Log</h2>
          <p className="mt-1 text-sm text-gray-500">Client-side filtered activity from the current status feed.</p>
        </div>
        <label className="flex items-center gap-3 rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm text-gray-600 shadow-xs">
          <span className="font-medium">Agent</span>
          <select className="form-select min-w-[180px] border-0 bg-transparent pr-8 focus:ring-0" value={selectedAgent} onChange={handleFilterChange}>
            {agentNames.map((agentName) => (
              <option key={agentName} value={agentName}>
                {agentName === 'all' ? 'All agents' : agentName}
              </option>
            ))}
          </select>
        </label>
      </div>

      <div className="rounded-xl bg-white shadow-xs">
        <div className="overflow-x-auto p-3">
          <table className="table-auto w-full">
            <thead className="rounded-xs bg-gray-50 text-xs uppercase text-gray-400">
              <tr>
                <th className="p-2 text-left font-semibold">Timestamp</th>
                <th className="p-2 text-left font-semibold">Agent</th>
                <th className="p-2 text-left font-semibold">Work Item ID</th>
                <th className="p-2 text-left font-semibold">Message</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 text-sm">
              {pageEntries.length ? (
                pageEntries.map((entry, index) => (
                  <tr key={`${entry.timestamp}-${index}`}>
                    <td className="p-2 text-gray-500">{formatTimestamp(entry.timestamp)}</td>
                    <td className="p-2 font-medium text-gray-900">{entry.agent}</td>
                    <td className="p-2">
                      <Link to={`/story/${entry.workItemId}`} className="font-semibold text-violet-500 hover:text-violet-600">
                        #{entry.workItemId}
                      </Link>
                    </td>
                    <td className="p-2 text-gray-700">{entry.message}</td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td className="p-6 text-center text-sm text-gray-500" colSpan="4">
                    No activity matches the current filter.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        <div className="flex items-center justify-between border-t border-gray-100 px-5 py-4 text-sm">
          <div className="text-gray-500">
            Page {page} of {totalPages}
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={() => setPage((current) => Math.max(1, current - 1))}
              disabled={page === 1}
              className="btn border-gray-200 bg-white text-gray-700 hover:border-gray-300 disabled:cursor-not-allowed disabled:opacity-50"
            >
              Previous
            </button>
            <button
              onClick={() => setPage((current) => Math.min(totalPages, current + 1))}
              disabled={page === totalPages}
              className="btn border-gray-200 bg-white text-gray-700 hover:border-gray-300 disabled:cursor-not-allowed disabled:opacity-50"
            >
              Next
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
