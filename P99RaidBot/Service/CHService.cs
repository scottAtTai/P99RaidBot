using Discord;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace P99RaidBot.Service
{
    public class ChainPosition
    {
        public int Order { get; set; }
        public bool Enabled { get; set; }
    }

    //tick is 1 second
    public class CHChainData : IDisposable
    {
        public const int MaxChainLength = 25;
        public required ChainPosition[] ChainOrder = new ChainPosition[MaxChainLength];
        public required AudioData AudioData { get; set; }
        public System.Timers.Timer? Timer { get; set; } = new System.Timers.Timer(1000);
        public int DelayInTicks = 4;
        public int TickCount = 0;
        public int CurrentIndex = 0;
        public required Action<CHChainData> Action;

        ~CHChainData()
        {
            Dispose();
        }
        public void Dispose()
        {
            Timer?.Stop();
            Timer?.Dispose();
            Timer = null;
        }
    }

    public class CHService
    {
        private readonly AudioService audioService;
        private readonly ILogger<CHService> logger;
        private readonly ConcurrentDictionary<ulong, CHChainData> chains = new();
        private readonly string assemblyDirectory = string.Empty;

        public CHService(AudioService audioService, ILogger<CHService> logger)
        {
            this.audioService = audioService;
            this.logger = logger;
            assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        public async void JoinVoiceChannel(ulong guild, IVoiceChannel target, Action<CHChainData> callback)
        {
            var data = await audioService.JoinAudioAsync(guild, target);
            var d = new CHChainData { AudioData = data, ChainOrder = new ChainPosition[CHChainData.MaxChainLength], Action = callback };
            _ = chains.TryAdd(guild, d);
            for (var i = 0; i < CHChainData.MaxChainLength; i++)
            {
                d.ChainOrder[i] = new ChainPosition { Order = i + 1, Enabled = false };
            }
            d.Timer.Elapsed += (s, e) => { Timer_Elapsed(d); };
            d.Timer.AutoReset = true;
            logger.Log(LogLevel.Information, $"Connected to voice on {target.Name}.");
        }

        private void Timer_Elapsed(CHChainData d)
        {
            var shouldPlaySound = d.DelayInTicks <= ++d.TickCount;
            Debug.WriteLine($"Timer_Elapsed: {d.TickCount}");
            if (shouldPlaySound)
            {
                if (!PlayNextSound(d))
                {
                    d.CurrentIndex = 0;
                    if (!PlayNextSound(d))
                    {
                        d.CurrentIndex = 0;
                    }
                }
            }
        }

        private bool PlayNextSound(CHChainData d)
        {
            Debug.WriteLine($"PlayNextSound: {d.CurrentIndex}");
            for (; d.CurrentIndex < d.ChainOrder.Length;)
            {
                var position = d.ChainOrder[d.CurrentIndex++];
                if (position.Enabled)
                {
                    PlaySound(d.CurrentIndex, d.AudioData.VoiceChannel.GuildId);
                    d.TickCount = 0;
                    d.Action?.Invoke(d);
                    return true;
                }
            }
            return false;
        }

        private void PlaySound(int position, ulong guildid)
        {
            Debug.WriteLine($"----PlaySound: {position}");
            var path = Path.Combine(assemblyDirectory, "numbers", $"{position}.mp3");
            _ = audioService.SendAudioAsync(guildid, path);
        }

        public void StartChain(ulong guild)
        {
            if (chains.TryGetValue(guild, out var chainData))
            {
                if (chainData.Timer.Enabled)
                {
                    return;
                }
                chainData.Timer.Enabled = true;
                chainData.CurrentIndex = 0;
            }
        }

        public void StopChain(ulong guild)
        {
            if (chains.TryGetValue(guild, out var chainData))
            {
                if (!chainData.Timer.Enabled)
                {
                    return;
                }
                chainData.Timer.Enabled = false;
                chainData.TickCount = chainData.CurrentIndex = 0;
            }
        }

        public CHChainData? ToggleChain(ulong guild, string orderstring)
        {
            if (chains.TryGetValue(guild, out var chainData))
            {
                if (int.TryParse(orderstring, out var order))
                {
                    var position = chainData.ChainOrder[order - 1];
                    position.Enabled = !position.Enabled;
                    return chainData;
                }
            }
            return null;
        }

        public async void LeaveVoiceChannel(ulong guild)
        {
            if (chains.TryRemove(guild, out var chainData))
            {
                await audioService.LeaveAudio(guild);
                chainData.Dispose();
                logger.Log(LogLevel.Information, $"Disconnected from voice on {chainData.AudioData.VoiceChannel.Name}.");
            }
        }
    }
}
