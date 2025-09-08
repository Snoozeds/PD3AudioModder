using System;
using System.Reflection;
using NAudio.Vorbis;
using NAudio.Wave;

/// <summary>
/// Class to handle audio playback using NAudio.
/// </summary>
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

    /// <summary>
    /// Plays an audio resource.
    /// </summary>
    /// <param name="resourceName"></param>
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

    /// <summary>
    /// Stops audio playback.
    /// </summary>
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
