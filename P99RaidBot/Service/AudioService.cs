using Discord;
using Discord.Audio;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace P99RaidBot.Service
{
    public class AudioData
    {
        public required AudioStream AudioStream { get; set; }
        public required IAudioClient AudioClient { get; set; }
        public required IVoiceChannel VoiceChannel { get; set; }
    }

    public class AudioService
    {
        private readonly ConcurrentDictionary<ulong, AudioData> connectedChannels = new();
        private readonly ILogger<AudioService> logger;
        public AudioService(ILogger<AudioService> logger)
        {
            this.logger = logger;
        }

        public async Task<AudioData> JoinAudioAsync(ulong guild, IVoiceChannel target)
        {
            if (connectedChannels.TryGetValue(guild, out var audioData))
            {
                return audioData;
            }

            var audioClient = await target.ConnectAsync();
            var data = new AudioData
            {
                AudioClient = audioClient,
                AudioStream = audioClient.CreatePCMStream(AudioApplication.Voice),
                VoiceChannel = target
            };
            if (connectedChannels.TryAdd(guild, data))
            {
                logger.Log(LogLevel.Information, $"Connected to voice on {target.Name}.");
            }
            return data;
        }

        public async Task LeaveAudio(ulong guild)
        {
            if (connectedChannels.TryRemove(guild, out var client))
            {
                await client.AudioClient.StopAsync();
                logger.Log(LogLevel.Information, $"Disconnected from voice on {client.VoiceChannel.Name}.");
            }
        }

        public async Task SendAudioAsync(ulong guild, string path)
        {
            if (connectedChannels.TryGetValue(guild, out var client))
            {
                await client.AudioClient.SetSpeakingAsync(true);
                using (var ffmpeg = CreateStream(path))
                using (var output = ffmpeg.StandardOutput.BaseStream)
                {
                    try
                    {
                        await output.CopyToAsync(client.AudioStream);
                        logger.Log(LogLevel.Information, $"STARTING playback of {path}");

                    }
                    catch (Exception ex)
                    {
                        logger.Log(LogLevel.Error, ex.ToString());
                    }
                    finally
                    {
                        await client.AudioStream.FlushAsync();
                        await client.AudioClient.SetSpeakingAsync(false);
                    }
                }
                logger.Log(LogLevel.Information, $"Done playback of {path}");
            }
        }

        private Process CreateStream(string path)
        {
            var command = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1";
            return Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg.exe",
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true
            });
        }
    }
}
