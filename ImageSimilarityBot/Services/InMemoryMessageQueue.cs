using ImageSimilarityBot.Interfaces;
using NetCord.Gateway;

namespace ImageSimilarityBot.Services;

public class InMemoryMessageQueue : IMessageQueue
{
    private Queue<Message> _messageQueue = new();

    public Task<Message?> DequeueMessageAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_messageQueue.Count > 0 ? _messageQueue.Dequeue() : null);
    }

    public Task EnqueueMessageAsync(Message message, CancellationToken cancellationToken)
    {
        _messageQueue.Enqueue(message);
        return Task.CompletedTask;
    }
}
