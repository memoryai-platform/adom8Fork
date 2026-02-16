using System.Text;
using System.Text.Json;
using AIAgents.Functions.Models;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;

namespace AIAgents.Functions.Services;

/// <summary>
/// Azure Storage Queue implementation of <see cref="IAgentTaskQueue"/>.
/// </summary>
public sealed class AgentTaskQueue : IAgentTaskQueue
{
    private readonly string _connectionString;

    public AgentTaskQueue(IConfiguration configuration)
    {
        _connectionString = configuration["AzureWebJobsStorage"]!;
    }

    public async Task EnqueueAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        var queueClient = new QueueClient(_connectionString, "agent-tasks");
        var messageJson = JsonSerializer.Serialize(task);
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(messageJson));
        await queueClient.SendMessageAsync(base64, cancellationToken);
    }

    public async Task<IReadOnlyList<AgentTask>> PeekAsync(int maxMessages = 32, CancellationToken cancellationToken = default)
    {
        var queueClient = new QueueClient(_connectionString, "agent-tasks");
        await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var peeked = await queueClient.PeekMessagesAsync(maxMessages, cancellationToken);
        var tasks = new List<AgentTask>();

        foreach (var msg in peeked.Value)
        {
            try
            {
                // Messages are base64-encoded JSON
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(msg.Body.ToString()));
                var task = JsonSerializer.Deserialize<AgentTask>(json);
                if (task != null)
                    tasks.Add(task);
            }
            catch
            {
                // Skip malformed messages
            }
        }

        return tasks;
    }
}
