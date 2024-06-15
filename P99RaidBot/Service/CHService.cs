using Discord;
using Microsoft.Extensions.Logging;

namespace P99RaidBot.Service
{
    public class CHService
    {
        private readonly AudioService audioService;
        private readonly ILogger<CHService> logger;
        public CHService(AudioService audioService, ILogger<CHService> logger)
        {
            this.audioService = audioService;
            this.logger = logger;
        }

        public async void JoinVoiceChannel(ulong guild, IVoiceChannel target)
        {
            _ = await audioService.JoinAudioAsync(guild, target);
            logger.Log(LogLevel.Information, $"Connected to voice on {target.Name}.");
        }

        public void StartChain()
        {

        }
    }
}
