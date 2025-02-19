using System;
using System.Reflection;
using NAudio.Vorbis;
using NAudio.Wave;

public class AudioPlayer : IDisposable
{
    private WaveOutEvent? outputDevice;
    private VorbisWaveReader? vorbisReader;
    private bool isPlaying = false;

    public AudioPlayer()
    {
        outputDevice = new WaveOutEvent();
        outputDevice.PlaybackStopped += OnPlaybackStopped;
    }

    public void PlaySound(string resourceName)
    {
        if (isPlaying)
        {
            Stop();
        }

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceStream = assembly.GetManifestResourceStream(
                $"PD3AudioModder.{resourceName}"
            );

            if (resourceStream == null)
            {
                throw new InvalidOperationException($"Resource {resourceName} not found.");
            }

            vorbisReader = new VorbisWaveReader(resourceStream);
            outputDevice?.Init(vorbisReader);
            outputDevice?.Play();
            isPlaying = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error playing audio: {ex.Message}");
            Dispose();
        }
    }

    public void Stop()
    {
        if (isPlaying)
        {
            outputDevice?.Stop();
            isPlaying = false;
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs args)
    {
        isPlaying = false;
    }

    public void Dispose()
    {
        Stop();
        outputDevice?.Dispose();
        outputDevice = null;
        vorbisReader?.Dispose();
        vorbisReader = null;

        GC.SuppressFinalize(this);
    }
}
