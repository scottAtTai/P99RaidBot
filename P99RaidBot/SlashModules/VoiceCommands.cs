using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using P99RaidBot.Core;
using P99RaidBot.Service;
using System.Diagnostics;

namespace P99RaidBot.SlashModules
{
    public class VoiceCommands : IBaseSlashCommand
    {
        private readonly ILogger<VoiceCommands> logger;
        private readonly CHService chService;
        private readonly DiscordSocketClient client;
        private readonly AudioService audioService;

        public const string joinchannel = nameof(joinchannel);
        public const string ButtonPlayId = nameof(ButtonPlayId);
        public const string ButtonStopId = nameof(ButtonStopId);
        public const string ButtonJoinVoiceId = nameof(ButtonJoinVoiceId);
        public const string ButtonLeaveVoiceId = nameof(ButtonLeaveVoiceId);

        public VoiceCommands(ILogger<VoiceCommands> logger, CHService chService, DiscordSocketClient client, AudioService audioService)
        {
            this.logger = logger;
            this.chService = chService;
            this.client = client; this.audioService = audioService;
        }

        public SlashCommandProperties CreateCommands(DiscordSocketClient client)
        {
            var guildCommand = new SlashCommandBuilder()
              .WithName(joinchannel)
              .WithDescription("Will add bot to text channel for management");
            logger.Log(LogLevel.Information, "JoinChannel command added to global commands");
            return guildCommand.Build();
        }

        public async Task<bool> Handle(SocketSlashCommand command)
        {
            if (command.Data.Name == joinchannel)
            {
                var embedBuilder = new EmbedBuilder().WithTitle("CH Chain Metronome");

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
                _ = await command.Channel.SendMessageAsync(embed: embedBuilder.Build(), components: BuildCHButtons(null).Build());
                _ = await command.Channel.SendMessageAsync(components: CreateControlButtons(false).Build());
                await command.RespondAsync($"Commands added to channel -- Have fun!", ephemeral: true);
                await command.DeleteOriginalResponseAsync();
                return true;
            }

            return false;
        }

        private ComponentBuilder BuildCHButtons(CHChainData? d)
        {
            var buttons = new ComponentBuilder();
            for (var i = 0; i < CHChainData.MaxChainLength; i++)
            {
                _ = buttons.WithButton((i + 1).ToString("000"), (i + 1).ToString("000"), d?.ChainOrder[i]?.Enabled == true ? ButtonStyle.Success : ButtonStyle.Danger, disabled: false, row: i / 10);
            }
            return buttons;
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
                .WithButton("Play", ButtonPlayId, ButtonStyle.Success, disabled: false)
                .WithButton("Stop", ButtonStopId, ButtonStyle.Danger, disabled: false)
                .WithSelectMenu(menuBuilder)
                .WithButton("Leave Voice", ButtonLeaveVoiceId, ButtonStyle.Success) :
                new ComponentBuilder()
                 .WithButton("Play", ButtonPlayId, ButtonStyle.Success, disabled: true)
                 .WithButton("Stop", ButtonStopId, ButtonStyle.Danger, disabled: true)
                .WithSelectMenu(menuBuilder)
             .WithButton("Join Voice", ButtonJoinVoiceId, ButtonStyle.Success);
        }

        public async Task<bool> Handle(SocketMessageComponent socketMessage)
        {
            var handled = false;
            if (socketMessage.GuildId.HasValue)
            {
                if (socketMessage.Data.CustomId == ButtonJoinVoiceId)
                {
                    await socketMessage.DeferAsync();
                    chService.JoinVoiceChannel(socketMessage.GuildId.Value, (socketMessage.User as IGuildUser).VoiceChannel, (d) =>
                    {
                        Debug.WriteLine($"JoinVoiceChannel Callback Fired: {d.CurrentIndex}");
                    });
                    var buttons = CreateControlButtons(true);
                    _ = await socketMessage.Channel.ModifyMessageAsync(socketMessage.Message.Id, m => m.Components = buttons.Build());

                    handled = true;
                }
                else if (socketMessage.Data.CustomId == ButtonPlayId)
                {
                    await socketMessage.DeferAsync();
                    chService.StartChain(socketMessage.GuildId.Value);
                    handled = true;
                }
                else if (socketMessage.Data.CustomId == ButtonStopId)
                {
                    await socketMessage.DeferAsync();
                    chService.StopChain(socketMessage.GuildId.Value);
                    handled = true;
                }
                else if (socketMessage.Data.CustomId == ButtonLeaveVoiceId)
                {
                    await socketMessage.DeferAsync();
                    chService.LeaveVoiceChannel(socketMessage.GuildId.Value);
                    var buttons = CreateControlButtons(false);
                    _ = await socketMessage.Channel.ModifyMessageAsync(socketMessage.Message.Id, m => m.Components = buttons.Build());
                    handled = true;
                }
                else if (socketMessage.Data.CustomId?.StartsWith("0") == true)
                {
                    await socketMessage.DeferAsync();
                    var chaindata = chService.ToggleChain(socketMessage.GuildId.Value, socketMessage.Data.CustomId);
                    if (chaindata != null)
                    {
                        _ = await socketMessage.Channel.ModifyMessageAsync(socketMessage.Message.Id, m => m.Components = BuildCHButtons(chaindata).Build());
                    }
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
