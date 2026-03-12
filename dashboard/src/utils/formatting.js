import { format, formatDistanceToNowStrict } from 'date-fns';

export function formatRelativeTime(value) {
  if (!value) {
    return 'Just now';
  }

  return formatDistanceToNowStrict(new Date(value), { addSuffix: true });
}

export function formatTimestamp(value) {
  if (!value) {
    return 'N/A';
  }

  return format(new Date(value), 'MMM d, yyyy h:mm a');
}

export function formatDuration(seconds) {
  if (seconds == null) {
    return 'N/A';
  }

  if (seconds < 60) {
    return `${Math.round(seconds)}s`;
  }

  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = Math.round(seconds % 60);

  if (minutes < 60) {
    return `${minutes}m ${remainingSeconds}s`;
  }

  const hours = Math.floor(minutes / 60);
  return `${hours}h ${minutes % 60}m`;
}

export function formatPercent(value) {
  if (value == null) {
    return '0%';
  }

  return `${Math.round(value * 100)}%`;
}
