using ImageSimilarityBot.Interfaces;
using NetCord.Gateway;
using System.Collections.Concurrent;

namespace ImageSimilarityBot.Services;

public class InMemoryMessageQueue : IMessageQueue
{
    private ConcurrentQueue<Message> _messageQueue = new();

    public Task<Message?> DequeueMessageAsync(CancellationToken cancellationToken)
    {
        _messageQueue.TryDequeue(out var message);
        return Task.FromResult(message);
    }

    public Task EnqueueMessageAsync(Message message, CancellationToken cancellationToken)
    {
        _messageQueue.Enqueue(message);
        return Task.CompletedTask;
    }
}
