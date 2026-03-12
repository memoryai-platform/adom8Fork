import Tooltip from '../mosaic/components/Tooltip';

/**
 * @param {{label: string, value: string, detail?: string, helpText?: string}} props
 */
export default function MetricCard({ detail, helpText, label, value }) {
  return (
    <div className="rounded-xl bg-white shadow-xs">
      <div className="flex h-full flex-col p-5">
        <div className="flex items-start justify-between">
          <div>
            <div className="text-xs font-semibold uppercase tracking-[0.16em] text-gray-400">{label}</div>
            <div className="mt-3 text-3xl font-semibold text-gray-900">{value}</div>
          </div>
          {helpText ? (
            <Tooltip size="sm" position="bottom">
              <div className="text-sm">{helpText}</div>
            </Tooltip>
          ) : null}
        </div>
        <div className="mt-auto pt-6 text-sm text-gray-500">{detail ?? 'Updated from the live orchestration feed.'}</div>
      </div>
    </div>
  );
}
