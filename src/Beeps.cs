using System.Media;

namespace MicMute;

sealed class Beeps : IDisposable
{
    const int SampleRate = 44100;

    SoundPlayer mutePlayer = null!;
    SoundPlayer unmutePlayer = null!;

    public Beeps(double volume) => Reload(volume);

    public void Reload(double volume)
    {
        Dispose();
        mutePlayer = CreatePlayer("mute.wav", [880, 440], volume);
        unmutePlayer = CreatePlayer("unmute.wav", [440, 880], volume);
    }

    public void Play(bool muted) => (muted ? mutePlayer : unmutePlayer).Play();

    static SoundPlayer CreatePlayer(string customFile, double[] frequencies, double volume)
    {
        // si el usuario deja su propio wav en la carpeta de config se usa ese
        var customPath = Path.Combine(Config.Folder, customFile);
        var player = File.Exists(customPath) ? new SoundPlayer(customPath) : new SoundPlayer(BuildWave(frequencies, volume));
        player.Load();
        return player;
    }

    static MemoryStream BuildWave(double[] frequencies, double volume)
    {
        const double toneSeconds = 0.07, gapSeconds = 0.02, fadeSeconds = 0.006;
        var toneSamples = (int)(SampleRate * toneSeconds);
        var gapSamples = (int)(SampleRate * gapSeconds);
        var fadeSamples = (int)(SampleRate * fadeSeconds);
        var amplitude = Math.Clamp(volume, 0, 1) * short.MaxValue;

        var samples = new List<short>();
        foreach (var frequency in frequencies)
        {
            for (var i = 0; i < toneSamples; i++)
            {
                var envelope = Math.Min(1.0, Math.Min(i, toneSamples - 1 - i) / (double)fadeSamples);
                samples.Add((short)(Math.Sin(2 * Math.PI * frequency * i / SampleRate) * amplitude * envelope));
            }
            samples.AddRange(new short[gapSamples]);
        }

        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            var dataBytes = samples.Count * 2;
            writer.Write("RIFF"u8);
            writer.Write(36 + dataBytes);
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(dataBytes);
            foreach (var sample in samples) writer.Write(sample);
        }
        stream.Position = 0;
        return stream;
    }

    public void Dispose()
    {
        mutePlayer?.Dispose();
        unmutePlayer?.Dispose();
    }
}
