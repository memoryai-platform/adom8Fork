namespace AIAgents.Functions.Services;

public interface IMergeEventDeduplicationStore
{
    Task<bool> TryProcessAsync(string dedupeKey, CancellationToken cancellationToken = default);
}
