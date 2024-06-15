using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using P99RaidBot.Core;
using P99RaidBot.Service;
using System.Reflection;

namespace P99RaidBot.SlashModules
{
    public class VoiceCommands : IBaseSlashCommand
    {
        private readonly ILogger<VoiceCommands> logger;
        private readonly CHService chService;
        private readonly DiscordSocketClient client;
        public VoiceCommands(ILogger<VoiceCommands> logger, CHService chService, DiscordSocketClient client)
        {
            this.logger = logger;
            this.chService = chService;
            this.client = client;
        }

        public SlashCommandProperties CreateCommands(DiscordSocketClient client)
        {
            var guildCommand = new SlashCommandBuilder()
              .WithName("joinchannel")
              .WithDescription("Will add bot to text channel for management");
            logger.Log(LogLevel.Information, "JoinChannel command added to global commands");
            return guildCommand.Build();
        }

        public async Task<bool> Handle(SocketSlashCommand command)
        {
            if (command.Data.Name == "joinchannel")
            {
                var embedBuilder = new EmbedBuilder().WithTitle("CH Chain Metronome");
                var buttons = new ComponentBuilder();
                for (var i = 1; i < 26; i++)
                {
                    _ = buttons.WithButton(i.ToString("000"), i.ToString("000"), ButtonStyle.Secondary, disabled: true, row: i / 10);
                }
                var oldmessages = await command.Channel.GetMessagesAsync().FlattenAsync();
                if (oldmessages != null)
                {
                    var userid = client.CurrentUser.Id;
                    foreach (var item in oldmessages)
                    {
                        if (item.Author.Id == userid)
                        {
                            await item.DeleteAsync();
                        }
                    }
                }
                _ = await command.Channel.SendMessageAsync(embed: embedBuilder.Build(), components: buttons.Build());
                buttons = CreateControlButtons(false);
                _ = await command.Channel.SendMessageAsync(components: buttons.Build());
                await command.RespondAsync($"Commands added to channel -- Have fun!", ephemeral: true);
                await command.DeleteOriginalResponseAsync();
                return true;
            }

            return false;
        }

        private ComponentBuilder CreateControlButtons(bool inVoice)
        {
            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Delay")
                .WithCustomId("Delay")
                .WithMinValues(1)
                .WithMaxValues(16);
            for (var i = 1; i < 17; i++)
            {
                _ = menuBuilder.AddOption($"{i} sec", i.ToString(), isDefault: i == 4);
            }

            return inVoice ? new ComponentBuilder()
                .WithButton("Play", "Play", ButtonStyle.Success, disabled: false)
                .WithButton("Stop", "Stop", ButtonStyle.Danger, disabled: false)
                .WithSelectMenu(menuBuilder)
                .WithButton("Leave Voice", "LeaveVoice", ButtonStyle.Success) :
                new ComponentBuilder()
                 .WithButton("Play", "Play", ButtonStyle.Success, disabled: true)
                 .WithButton("Stop", "Stop", ButtonStyle.Danger, disabled: true)
                .WithSelectMenu(menuBuilder)
             .WithButton("Join Voice", "JoinVoice", ButtonStyle.Success);
        }
        public static string AssemblyDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public async Task<bool> Handle(SocketMessageComponent socketMessage)
        {
            var handled = false;
            if (socketMessage.GuildId.HasValue)
            {
                if (socketMessage.Data.CustomId == "JoinVoice")
                {
                    await socketMessage.DeferAsync();
                    chService.JoinVoiceChannel(socketMessage.GuildId.Value, (socketMessage.User as IGuildUser).VoiceChannel);
                    var buttons = CreateControlButtons(true);
                    _ = await socketMessage.Channel.ModifyMessageAsync(socketMessage.Message.Id, m => m.Components = buttons.Build());

                    handled = true;
                }
                else if (socketMessage.Data.CustomId == "Play")
                {
                    await socketMessage.DeferAsync();
                    var path = Path.Combine(AssemblyDirectory, "numbers", "1.mp3");
                    //   _ = chService.SendAudioAsync(socketMessage.GuildId.Value, socketMessage.Channel, path); 
                    handled = true;
                }
                else if (socketMessage.Data.CustomId == "Stop")
                {
                    await socketMessage.DeferAsync();
                    //    _ = service.LeaveAudio(socketMessage.GuildId.Value);
                    handled = true;
                }
                else if (socketMessage.Data.CustomId == "LeaveVoice")
                {
                    await socketMessage.DeferAsync();
                    //   _ = service.LeaveAudio(socketMessage.GuildId.Value);
                    var buttons = CreateControlButtons(false);
                    _ = await socketMessage.Channel.ModifyMessageAsync(socketMessage.Message.Id, m => m.Components = buttons.Build());
                    handled = true;
                }
            }
            return handled;
        }

        //[SlashCommand("join management channel", "Will add the RaidBot to channel so it can be managed here.")]
        //public void JoinManagementChannel()
        //{
        //    _ = Context.Channel.SendMessageAsync("Joining voice channel");
        //}

        //[SlashCommand("join voice", "Will add the RaidBot to the voice channel you are currently in")]
        //public async Task JoinVoice()
        //{
        //    await service.JoinAudio(Context.Guild, (Context.User as IVoiceState).VoiceChannel);
        //}

        //[SlashCommand("leave voice", "Will add the RaidBot to the voice channel you are currently in")]
        //public async Task LeaveVoice()
        //{
        //    await service.LeaveAudio(Context.Guild);
        //}
    }
}
