using System.IO;
using NAudio.Wave;

namespace AttentionLooper.Models;

public sealed class ChimeController : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _loopCts;
    private readonly Func<string?> _getSoundPath;
    private readonly Action<string> _log;
    private int _isSoundPlaying;

    public PlayerState State { get; private set; } = PlayerState.Stopped;
    public TimeSpan Period { get; private set; } = TimeSpan.FromMinutes(4);
    public DateTime? NextFireTimeUtc { get; private set; }
    public float Volume { get; set; } = 0.65f;

    public event Action? StateChanged;
    public event Action<string>? SoundPlaybackError;
    public event Action<double>? PlaybackProgressChanged;
    public event Action? PlaybackFinished;

    public ChimeController(Func<string?> getSoundPath, Action<string> log)
    {
        _getSoundPath = getSoundPath;
        _log = log;
    }

    public void SetPeriod(TimeSpan period)
    {
        if (period <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be > 0.");

        lock (_gate)
        {
            Period = period;
            if (State == PlayerState.Playing)
            {
                StopLoop_NoLock();
                StartLoop_NoLock();
            }
        }
        StateChanged?.Invoke();
    }

    public void Play()
    {
        lock (_gate)
        {
            if (State == PlayerState.Playing) return;
            State = PlayerState.Playing;
            StartLoop_NoLock();
        }
        StateChanged?.Invoke();
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (State == PlayerState.Stopped) return;
            State = PlayerState.Stopped;
            StopLoop_NoLock();
        }
        StateChanged?.Invoke();
    }

    private void StartLoop_NoLock()
    {
        _loopCts = new CancellationTokenSource();
        NextFireTimeUtc = DateTime.UtcNow + Period;

        var cts = _loopCts;
        _ = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var now = DateTime.UtcNow;
                    var target = NextFireTimeUtc;
                    if (target == null) return;

                    var delay = target.Value - now;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cts.Token).ConfigureAwait(false);
                    }

                    if (cts.Token.IsCancellationRequested) return;

                    _ = PlaySoundAsync(cts.Token);

                    NextFireTimeUtc = DateTime.UtcNow + Period;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _log($"Timer loop error: {ex.Message}");
            }
        });
    }

    private void StopLoop_NoLock()
    {
        NextFireTimeUtc = null;
        try { _loopCts?.Cancel(); } catch { }
        _loopCts?.Dispose();
        _loopCts = null;
    }

    private async Task PlaySoundAsync(CancellationToken token)
    {
        if (Interlocked.Exchange(ref _isSoundPlaying, 1) == 1)
            return;

        try
        {
            var path = _getSoundPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                SoundPlaybackError?.Invoke(path == null
                    ? "No sound selected."
                    : $"Sound file not found: {Path.GetFileName(path)}");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    using var audioFile = new AudioFileReader(path) { Volume = Volume };
                    using var outputDevice = new WaveOutEvent();
                    outputDevice.Init(audioFile);
                    outputDevice.Play();

                    var totalSeconds = audioFile.TotalTime.TotalSeconds;

                    while (outputDevice.PlaybackState == PlaybackState.Playing)
                    {
                        if (token.IsCancellationRequested) break;
                        if (totalSeconds > 0)
                        {
                            var progress = Math.Clamp(audioFile.CurrentTime.TotalSeconds / totalSeconds, 0.0, 1.0);
                            PlaybackProgressChanged?.Invoke(progress);
                        }
                        Thread.Sleep(50);
                    }

                    if (!token.IsCancellationRequested)
                        PlaybackFinished?.Invoke();
                }
                catch (Exception ex)
                {
                    SoundPlaybackError?.Invoke($"Audio error: {ex.Message}");
                }
            }, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SoundPlaybackError?.Invoke($"Playback error: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isSoundPlaying, 0);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            StopLoop_NoLock();
        }
    }
}
