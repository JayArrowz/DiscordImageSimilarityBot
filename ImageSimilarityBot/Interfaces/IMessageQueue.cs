using NetCord.Gateway;

namespace ImageSimilarityBot.Interfaces;

public interface IMessageQueue
{
    Task EnqueueMessageAsync(Message message, CancellationToken cancellationToken);
    Task<Message?> DequeueMessageAsync(CancellationToken cancellationToken);
}
