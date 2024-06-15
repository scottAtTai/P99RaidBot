using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace P99RaidBot.Core
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient client;
        private readonly IServiceProvider services;
        private readonly List<IBaseSlashCommand> baseSlashCommands;
        private readonly IOptions<Settings> options;
        private readonly ILogger<InteractionHandler> logger;
        public InteractionHandler(DiscordSocketClient client, IServiceProvider services, IEnumerable<IBaseSlashCommand> baseSlashCommands, IOptions<Settings> options, ILogger<InteractionHandler> logger)
        {
            this.client = client;
            this.services = services;
            this.options = options;
            this.baseSlashCommands = baseSlashCommands.ToList();
            this.client.Ready += Client_Ready;
            this.logger = logger;
        }

        private async Task Client_Ready()
        {
            var commands = new List<SlashCommandProperties>();
            foreach (var item in baseSlashCommands)
            {

                commands.Add(item.CreateCommands(client));
            }

            try
            {
                if (IsDebug())
                {
                    _ = await client.Rest.BulkOverwriteGuildCommands(commands.ToArray(), options.Value.testGuild);
                }
                else
                {
                    _ = await client.Rest.BulkOverwriteGlobalCommands(commands.ToArray());
                }
            }
            catch (HttpException exception)
            {
                var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);
                Console.WriteLine(json);
            }
            client.SlashCommandExecuted += SlashCommandExecuted;
            client.ButtonExecuted += ClientButtonExecuted;
        }

        private async Task ClientButtonExecuted(SocketMessageComponent arg)
        {
            foreach (var item in baseSlashCommands)
            {
                if (await item.Handle(arg))
                {
                    break;
                }
            }
        }

        private static bool IsDebug()
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }

        private async Task SlashCommandExecuted(SocketSlashCommand arg)
        {
            foreach (var item in baseSlashCommands)
            {
                if (await item.Handle(arg))
                {
                    break;
                }
            }
        }
    }
}
