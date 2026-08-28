using System.Diagnostics;
using System.Runtime.InteropServices;
using Tractus.Ndi;

namespace Tractus.PresenterTest;

public sealed class PresenterSender : IDisposable
{
    private static readonly double[] Frequencies = [400, 500, 630, 800, 1000, 1250, 1600, 2000];

    private readonly int _number;
    private readonly double _toneAmplitude;
    private readonly CancellationToken _stop;
    private Thread? _thread;
    private nint _sender;
    private nint _videoBuffer;
    private nint _audioBuffer;
    private int _audioMode = (int)AudioMode.Silence;
    private long _framesSent;
    private double _measuredFps;
    private int _connections;
    private double _phase;
    private double _currentGain;

    private readonly string? _imagePath;

    public PresenterSender(int number, string prefix, double toneLevelDb, string? imagePath, CancellationToken stop)
    {
        _number = number;
        SourceName = $"{prefix} {number}";
        ToneFrequency = Frequencies[number - 1];
        _toneAmplitude = Math.Pow(10, toneLevelDb / 20.0);
        _imagePath = imagePath;
        _stop = stop;
    }

    public int Number => _number;
    public string SourceName { get; }
    public double ToneFrequency { get; }
    public Exception? Failure { get; private set; }
    public long FramesSent => Interlocked.Read(ref _framesSent);
    public double MeasuredFps => Volatile.Read(ref _measuredFps);
    public int Connections => Volatile.Read(ref _connections);
    public AudioMode AudioMode => (AudioMode)Volatile.Read(ref _audioMode);
    public bool IsRunning => _thread is { IsAlive: true } && Failure is null;

    public void SetAudioMode(AudioMode mode) => Volatile.Write(ref _audioMode, (int)mode);

    public void Start()
    {
        if (_thread is not null) return;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = $"Tractus Presenter {_number}",
            Priority = ThreadPriority.AboveNormal
        };
        _thread.Start();
    }

    private unsafe void Run()
    {
        try
        {
            using var ndiName = new NDIInteropString(SourceName);
            using var noGroups = new NDIInteropString(null);
            var create = new send_create_t
            {
                p_ndi_name = ndiName,
                p_groups = noGroups,
                clock_video = true,
                clock_audio = false
            };

            _sender = NDIWrapper.send_create(ref create);
            if (_sender == nint.Zero)
                throw new InvalidOperationException("NDI could not create the source.");

            var pixels = PresenterImageLoader.LoadBgra(_number, VideoSpec.Width, VideoSpec.Height, _imagePath);
            _videoBuffer = Marshal.AllocHGlobal(pixels.Length);
            Marshal.Copy(pixels, 0, _videoBuffer, pixels.Length);

            var audioBytes = VideoSpec.AudioSamplesPerFrame * VideoSpec.AudioChannels * sizeof(float);
            _audioBuffer = Marshal.AllocHGlobal(audioBytes);

            var video = new video_frame_v2_t
            {
                xres = VideoSpec.Width,
                yres = VideoSpec.Height,
                FourCC = FourCC_type_e.FourCC_type_BGRA,
                frame_rate_N = VideoSpec.Fps,
                frame_rate_D = 1,
                picture_aspect_ratio = 16f / 9f,
                frame_format_type = frame_format_type_e.frame_format_type_progressive,
                timecode = NDIWrapper.send_timecode_synthesize,
                p_data = _videoBuffer,
                line_stride_in_bytes = VideoSpec.Width * 4,
                p_metadata = nint.Zero,
                timestamp = 0
            };

            var audio = new audio_frame_v2_t
            {
                sample_rate = VideoSpec.AudioSampleRate,
                no_channels = VideoSpec.AudioChannels,
                no_samples = VideoSpec.AudioSamplesPerFrame,
                timecode = NDIWrapper.send_timecode_synthesize,
                p_data = _audioBuffer,
                channel_stride_in_bytes = VideoSpec.AudioSamplesPerFrame * sizeof(float),
                p_metadata = nint.Zero,
                timestamp = 0
            };

            var rateClock = Stopwatch.StartNew();
            var rateFrameStart = 0L;
            while (!_stop.IsCancellationRequested)
            {
                video.timecode = NDIWrapper.send_timecode_synthesize;
                NDIWrapper.send_send_video_v2(_sender, ref video);
                Interlocked.Increment(ref _framesSent);

                var mode = AudioMode;
                FillAudio((float*)_audioBuffer, mode);
                if (mode != AudioMode.NoAudio)
                {
                    audio.timecode = NDIWrapper.send_timecode_synthesize;
                    NDIWrapper.send_send_audio_v2(_sender, ref audio);
                }

                if (rateClock.ElapsedMilliseconds >= 1000)
                {
                    var frames = FramesSent;
                    Volatile.Write(ref _measuredFps, (frames - rateFrameStart) / rateClock.Elapsed.TotalSeconds);
                    rateFrameStart = frames;
                    rateClock.Restart();
                    Volatile.Write(ref _connections, NDIWrapper.send_get_no_connections(_sender, 0));
                }
            }
        }
        catch (Exception ex)
        {
            Failure = ex;
        }
        finally
        {
            CleanupNative();
        }
    }

    private unsafe void FillAudio(float* destination, AudioMode mode)
    {
        var targetGain = mode == AudioMode.Tone ? _toneAmplitude : 0.0;
        var gainStep = 1.0 / Math.Max(1, VideoSpec.AudioSampleRate * 0.005);
        var phaseStep = 2 * Math.PI * ToneFrequency / VideoSpec.AudioSampleRate;

        for (var sample = 0; sample < VideoSpec.AudioSamplesPerFrame; sample++)
        {
            if (_currentGain < targetGain) _currentGain = Math.Min(targetGain, _currentGain + gainStep);
            else if (_currentGain > targetGain) _currentGain = Math.Max(targetGain, _currentGain - gainStep);

            var value = (float)(Math.Sin(_phase) * _currentGain);
            destination[sample] = value;
            destination[VideoSpec.AudioSamplesPerFrame + sample] = value;
            _phase += phaseStep;
            if (_phase >= 2 * Math.PI) _phase -= 2 * Math.PI;
        }
    }

    public void Dispose()
    {
        if (_thread is { IsAlive: true }) _thread.Join(TimeSpan.FromSeconds(3));
        CleanupNative();
    }

    private void CleanupNative()
    {
        var sender = Interlocked.Exchange(ref _sender, nint.Zero);
        if (sender != nint.Zero) NDIWrapper.send_destroy(sender);
        var video = Interlocked.Exchange(ref _videoBuffer, nint.Zero);
        if (video != nint.Zero) Marshal.FreeHGlobal(video);
        var audio = Interlocked.Exchange(ref _audioBuffer, nint.Zero);
        if (audio != nint.Zero) Marshal.FreeHGlobal(audio);
    }
}
