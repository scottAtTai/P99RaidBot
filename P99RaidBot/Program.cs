using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using P99RaidBot;
using P99RaidBot.Core;
using P99RaidBot.Service;
using System.Diagnostics;
using System.Reflection;

using (var services = ConfigureServices())
{
    var client = services.GetRequiredService<DiscordSocketClient>();
    _ = services.GetRequiredService<InteractionHandler>();
    client.Log += LogAsync;
    services.GetRequiredService<CommandService>().Log += LogAsync;

    // Subscribe to client log events
    client.Log += m =>
    {
        Debug.WriteLine($"client Log: {m}");
        return Task.CompletedTask;
    };
    // Subscribe to slash command log events
    var commands = services.GetRequiredService<InteractionService>();
    commands.Log += m =>
    {
        Debug.WriteLine($"Slash Log: {m}");
        return Task.CompletedTask;
    };
    var options = services.GetRequiredService<IOptions<Settings>>();
    await client.LoginAsync(TokenType.Bot, options.Value.botToken);
    await client.StartAsync();
    await Task.Delay(Timeout.Infinite);
}

static Task LogAsync(LogMessage log)
{
    Console.WriteLine(log.ToString());
    return Task.CompletedTask;
}

static ServiceProvider ConfigureServices()
{
    var configuration = new ConfigurationBuilder()
        .AddUserSecrets(Assembly.GetExecutingAssembly())
        .Build();

    var services = new ServiceCollection()
        .Configure<Settings>(configuration.GetSection("settings"))
        .AddOptions();

    _ = services.AddSingleton(provider =>
    {
        var config = new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.AllUnprivileged,
            LogLevel = LogSeverity.Debug
        };
        var client = new DiscordSocketClient(config);
        return client;
    });
    _ = services.AddLogging(builder => builder.AddConsole());
    _ = services.AddSingleton<CommandService>();
    _ = services.AddSingleton<AudioService>();
    _ = services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
    _ = services.AddSingleton<InteractionHandler>();
    _ = services.AddSingleton<CHService>();

    var typesFromAssemblies = Assembly.GetExecutingAssembly().DefinedTypes.Where(x => x.GetInterfaces().Contains(typeof(IBaseSlashCommand)));
    foreach (var type in typesFromAssemblies)
    {
        services.Add(new ServiceDescriptor(typeof(IBaseSlashCommand), type, ServiceLifetime.Singleton));
    }

    return services.BuildServiceProvider();
}