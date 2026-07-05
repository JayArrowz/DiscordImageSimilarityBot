using ImageSimilarityBot.Interfaces;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace ImageSimilarityBot.Services;

public class IncomingMessageHandler : IMessageCreateGatewayHandler
{
    private readonly IMessageQueue _messageQueue;

    public IncomingMessageHandler(IMessageQueue messageQueue)
    {
        _messageQueue = messageQueue;
    }

    public async ValueTask HandleAsync(Message arg)
    {
        var attachments = arg.Attachments.Any(t => t.ContentType?.StartsWith("image/") ?? false);
        if(arg.Author.IsBot == false && (attachments || arg.Content.ToLower().Contains("http")))
            await _messageQueue.EnqueueMessageAsync(arg, CancellationToken.None);
    }
}
