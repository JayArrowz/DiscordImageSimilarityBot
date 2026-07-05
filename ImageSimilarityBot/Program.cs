using ImageSimilarityBot.Interfaces;
using ImageSimilarityBot.Model;
using ImageSimilarityBot.Services;
using Microsoft.EntityFrameworkCore;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Services.ApplicationCommands;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddDiscordGateway(options =>
    {
        options.Intents = GatewayIntents.GuildMessages |
        GatewayIntents.MessageContent |
        GatewayIntents.DirectMessages |
        GatewayIntents.GuildModeration;
    })
    .AddDbContext<ImageSimilarityContext>(o =>
    {
        o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), npgsql => npgsql.UseVector().MigrationsAssembly(typeof(ImageSimilarityContext).Assembly.FullName));
    })
    .Configure<AIConfig>(builder.Configuration.GetSection("AIConfig"))
    .AddHostedService<QueueHandlerBackgroundService>()
    .AddSingleton<IMessageQueue, InMemoryMessageQueue>()
    .AddScoped<SourceImageHandler>()
    .AddSingleton<VllmEmbeddingService>()
    .AddSingleton<IHasher, Sha256Hasher>()
    .AddLogging()
    .AddGatewayHandler<IncomingMessageHandler>()
    .AddApplicationCommands()
    .AddHttpClient();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ImageSimilarityContext>();
    await dbContext.Database.MigrateAsync(CancellationToken.None);

    var sourceImageHandler = scope.ServiceProvider.GetRequiredService<SourceImageHandler>();
    await sourceImageHandler.HashAndEmbedSourceImagesAsync(CancellationToken.None);
}

host.AddSlashCommand("refresh", "Refreshes the source images and re-embeds them.", async () =>
{
    using var scope = host.Services.CreateScope();
    var sourceImageHandler = scope.ServiceProvider.GetRequiredService<SourceImageHandler>();
    await sourceImageHandler.HashAndEmbedSourceImagesAsync(CancellationToken.None);
    var dbContext = scope.ServiceProvider.GetRequiredService<ImageSimilarityContext>();
    var count = await dbContext.SourceImages.CountAsync(CancellationToken.None);
    return $"Source images refreshed and re-embedded. Total: {count}";
});


host.AddSlashCommand("add", "Adds a source image.", async (

    [SlashCommandParameter(Name = "uri", Description = "Image URL")]
    string uri,
    [SlashCommandParameter(Name = "bannable", Description = "Weather the image is bannable")]
    bool bannable,
    [SlashCommandParameter(Name = "threshold", Description = "Similiarty threshold 0.0-1.0, higher the number the closer match a image has to be, to be actioned", MinValue = 0.0, MaxValue = 1.0)]
    double threshold) =>
{
    using var scope = host.Services.CreateScope();
    var sourceImageHandler = scope.ServiceProvider.GetRequiredService<SourceImageHandler>();
    return await sourceImageHandler.AddOrUpdateImageAsync(uri, bannable, threshold, CancellationToken.None);
});


await host.RunAsync();
