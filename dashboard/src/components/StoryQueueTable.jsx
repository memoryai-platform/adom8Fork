import { Link } from 'react-router-dom';

/**
 * @param {{queue: Array<{id: number, title: string, autonomyLevel: number, queuePosition: number}>}} props
 */
export default function StoryQueueTable({ queue }) {
  const rows = (queue ?? []).slice(0, 10);

  return (
    <div className="col-span-full xl:col-span-7 rounded-xl bg-white shadow-xs">
      <header className="border-b border-gray-100 px-5 py-4">
        <h2 className="font-semibold text-gray-800">Story Queue</h2>
      </header>
      <div className="p-3">
        {rows.length ? (
          <div className="overflow-x-auto">
            <table className="table-auto w-full">
              <thead className="rounded-xs bg-gray-50 text-xs uppercase text-gray-400">
                <tr>
                  <th className="p-2 text-left font-semibold">Work Item</th>
                  <th className="p-2 text-left font-semibold">Title</th>
                  <th className="p-2 text-center font-semibold">Autonomy</th>
                  <th className="p-2 text-center font-semibold">Queue Position</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 text-sm font-medium">
                {rows.map((item) => (
                  <tr key={item.id}>
                    <td className="p-2">
                      <Link
                        to={`/story/${item.id}`}
                        state={{ story: item }}
                        className="font-semibold text-violet-500 hover:text-violet-600"
                      >
                        #{item.id}
                      </Link>
                    </td>
                    <td className="p-2 text-gray-800">{item.title}</td>
                    <td className="p-2 text-center">
                      <span className="inline-flex rounded-full bg-violet-100 px-2.5 py-1 text-xs font-semibold text-violet-700">
                        L{item.autonomyLevel}
                      </span>
                    </td>
                    <td className="p-2 text-center text-gray-600">{item.queuePosition}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div className="rounded-lg border border-dashed border-gray-200 px-4 py-8 text-center text-sm text-gray-500">
            Queue is empty
          </div>
        )}
      </div>
    </div>
  );
}
