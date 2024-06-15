using Discord;
using Discord.WebSocket;

namespace P99RaidBot.Core
{
    public interface IBaseSlashCommand
    {
        SlashCommandProperties CreateCommands(DiscordSocketClient client);
        Task<bool> Handle(SocketSlashCommand command);
        Task<bool> Handle(SocketMessageComponent socketMessage);
    }
}
