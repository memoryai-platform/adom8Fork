import { useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useOutletContext, useParams } from 'react-router-dom';

import { getStoryDetail } from '../api';
import { buildFallbackStoryDetail } from '../utils/story';
import { formatRelativeTime, formatTimestamp } from '../utils/formatting';

function phaseStyles(status) {
  switch (status) {
    case 'completed':
      return 'bg-green-100 text-green-700 border-green-200';
    case 'active':
      return 'bg-sky-100 text-sky-700 border-sky-200';
    case 'failed':
      return 'bg-red-100 text-red-700 border-red-200';
    default:
      return 'bg-gray-100 text-gray-600 border-gray-200';
  }
}

export default function StoryDetail() {
  const { id } = useParams();
  const location = useLocation();
  const { appKey, data: statusData } = useOutletContext();
  const [detail, setDetail] = useState(() => buildFallbackStoryDetail(id, statusData, location.state?.story));
  const [detailsUnavailable, setDetailsUnavailable] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function loadDetail() {
      setLoading(true);
      setDetailsUnavailable(false);

      try {
        const response = await getStoryDetail(id, appKey);

        if (cancelled) {
          return;
        }

        if (!response) {
          setDetailsUnavailable(true);
          setDetail(buildFallbackStoryDetail(id, statusData, location.state?.story));
          return;
        }

        setDetail(response);
      } catch (error) {
        if (cancelled) {
          return;
        }

        if (error.code === 404) {
          setDetailsUnavailable(true);
          setDetail(buildFallbackStoryDetail(id, statusData, location.state?.story));
          return;
        }

        setDetailsUnavailable(true);
        setDetail(buildFallbackStoryDetail(id, statusData, location.state?.story));
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    loadDetail();

    return () => {
      cancelled = true;
    };
  }, [appKey, id, location.state?.story, statusData]);

  const metadata = useMemo(() => detail ?? buildFallbackStoryDetail(id, statusData, location.state?.story), [detail, id, location.state?.story, statusData]);

  if (loading && !metadata) {
    return <div className="rounded-xl bg-white px-5 py-8 text-sm text-gray-500 shadow-xs">Loading story details...</div>;
  }

  if (!metadata) {
    return <div className="rounded-xl border border-dashed border-gray-200 bg-white px-5 py-8 text-sm text-gray-500 shadow-xs">Story details not found.</div>;
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <Link to="/" className="text-sm font-medium text-violet-500 hover:text-violet-600">
            Back to Overview
          </Link>
          <h2 className="mt-2 text-2xl font-semibold text-gray-900">
            #{metadata.id} {metadata.title}
          </h2>
        </div>
        <div className="flex flex-wrap gap-2 text-sm">
          <span className="rounded-full bg-gray-100 px-3 py-1 font-semibold text-gray-700">{metadata.state}</span>
          <span className="rounded-full bg-violet-100 px-3 py-1 font-semibold text-violet-700">Autonomy L{metadata.autonomyLevel}</span>
        </div>
      </div>

      {detailsUnavailable ? (
        <div className="rounded-xl border border-yellow-200 bg-yellow-50 px-4 py-3 text-sm text-yellow-700">
          Details not available from the backend yet. Showing the best available status snapshot.
        </div>
      ) : null}

      <div className="grid grid-cols-12 gap-6">
        <div className="col-span-full rounded-xl bg-white p-5 shadow-xs xl:col-span-4">
          <h3 className="text-sm font-semibold uppercase tracking-[0.16em] text-gray-400">Story Summary</h3>
          <dl className="mt-4 space-y-4 text-sm">
            <div>
              <dt className="text-gray-400">Current Agent</dt>
              <dd className="mt-1 font-semibold text-gray-900">{metadata.currentAgent ?? 'Pending assignment'}</dd>
            </div>
            <div>
              <dt className="text-gray-400">Last Agent</dt>
              <dd className="mt-1 font-semibold text-gray-900">{metadata.lastAgent ?? 'None yet'}</dd>
            </div>
            <div>
              <dt className="text-gray-400">Created</dt>
              <dd className="mt-1 font-semibold text-gray-900">{formatTimestamp(metadata.createdDate)}</dd>
            </div>
            <div>
              <dt className="text-gray-400">Updated</dt>
              <dd className="mt-1 font-semibold text-gray-900">{formatTimestamp(metadata.updatedDate)}</dd>
            </div>
          </dl>
        </div>

        <div className="col-span-full rounded-xl bg-white p-5 shadow-xs xl:col-span-8">
          <div className="mb-4 flex items-center justify-between">
            <h3 className="font-semibold text-gray-800">Agent Phase Timeline</h3>
            <span className="text-sm text-gray-400">Latest update {formatRelativeTime(metadata.updatedDate)}</span>
          </div>
          <ul className="space-y-4">
            {(metadata.phases ?? []).map((phase, index) => (
              <li key={phase.agent} className="relative pb-4 last:pb-0">
                <div className="pl-8">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <div>
                      <div className="text-sm font-semibold text-gray-900">{phase.agent.replace('Agent', ' Agent')}</div>
                      <div className="mt-1 text-xs text-gray-500">
                        {phase.completedAt ? `Completed ${formatRelativeTime(phase.completedAt)}` : 'Awaiting execution'}
                      </div>
                    </div>
                    <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold capitalize ${phaseStyles(phase.status)}`}>
                      {phase.status}
                    </span>
                  </div>
                </div>
                <div aria-hidden="true">
                  {index < (metadata.phases?.length ?? 0) - 1 ? (
                    <div className="absolute bottom-0 left-1.5 top-0.5 ml-px w-0.5 bg-gray-200" />
                  ) : null}
                  <div className="absolute left-0 top-1.5 h-3 w-3 rounded-full border-2 border-white bg-violet-500" />
                </div>
              </li>
            ))}
          </ul>
        </div>

        <div className="col-span-full rounded-xl bg-white shadow-xs">
          <header className="border-b border-gray-100 px-5 py-4">
            <h3 className="font-semibold text-gray-800">Story Activity</h3>
          </header>
          <div className="p-3">
            {(metadata.activity ?? []).length ? (
              <div className="overflow-x-auto">
                <table className="table-auto w-full">
                  <thead className="rounded-xs bg-gray-50 text-xs uppercase text-gray-400">
                    <tr>
                      <th className="p-2 text-left font-semibold">Timestamp</th>
                      <th className="p-2 text-left font-semibold">Agent</th>
                      <th className="p-2 text-left font-semibold">Message</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100 text-sm">
                    {metadata.activity.map((entry, index) => (
                      <tr key={`${entry.timestamp}-${index}`}>
                        <td className="p-2 text-gray-500">{formatTimestamp(entry.timestamp)}</td>
                        <td className="p-2 font-medium text-gray-900">{entry.agent}</td>
                        <td className="p-2 text-gray-700">{entry.message}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="rounded-lg border border-dashed border-gray-200 px-4 py-8 text-center text-sm text-gray-500">
                No activity is available for this story yet.
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
