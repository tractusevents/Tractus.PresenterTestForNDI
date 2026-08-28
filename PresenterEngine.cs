using System.Diagnostics;

namespace Tractus.PresenterTest;

public sealed class PresenterEngine : IDisposable
{
    private readonly object _gate = new();
    private readonly AppSettings _settings;
    private CancellationTokenSource? _stop;
    private PresenterSender[] _senders = [];
    private Timer? _chaseTimer;
    private Stopwatch _chaseClock = Stopwatch.StartNew();
    private int _chaseIndex;
    private AudioProgramMode _programMode;

    public PresenterEngine(AppSettings settings)
    {
        _settings = settings;
        _settings.Normalize();
        _programMode = settings.AudioProgram;
        NdiRuntime.EnsureInitialized();
    }

    public IReadOnlyList<PresenterSender> Senders => _senders;
    public bool IsRunning => _senders.Length > 0;
    public AudioProgramMode ProgramMode => _programMode;
    public int ChasePresenterNumber => _programMode == AudioProgramMode.ToneChase && _senders.Length > 0 ? _chaseIndex + 1 : 0;

    public void Start()
    {
        lock (_gate)
        {
            if (IsRunning) return;
            _stop = new CancellationTokenSource();
            _senders = Enumerable.Range(1, _settings.SourceCount)
                .Select(number => new PresenterSender(
                    number,
                    _settings.NamePrefix,
                    _settings.ToneLevelDb,
                    _settings.CustomImagePaths[number - 1],
                    _stop.Token))
                .ToArray();
            foreach (var sender in _senders) sender.Start();
            _chaseIndex = 0;
            _chaseClock.Restart();
            ApplyAudioModes();
            _chaseTimer = new Timer(_ => ChaseTick(), null, 100, 100);
        }
    }

    public void Stop()
    {
        PresenterSender[] senders;
        lock (_gate)
        {
            _chaseTimer?.Dispose();
            _chaseTimer = null;
            _stop?.Cancel();
            senders = _senders;
            _senders = [];
        }
        foreach (var sender in senders) sender.Dispose();
        _stop?.Dispose();
        _stop = null;
    }

    public void SetProgramMode(AudioProgramMode mode)
    {
        lock (_gate)
        {
            _programMode = mode;
            _settings.AudioProgram = mode;
            _chaseIndex = 0;
            _chaseClock.Restart();
            ApplyAudioModes();
        }
    }

    public void SetIndividualMode(int number, AudioMode mode)
    {
        if (number is < 1 or > 8) return;
        _settings.SourceAudioModes[number - 1] = mode;
        if (_programMode == AudioProgramMode.Individual)
            _senders.FirstOrDefault(sender => sender.Number == number)?.SetAudioMode(mode);
    }

    private void ChaseTick()
    {
        lock (_gate)
        {
            if (_programMode != AudioProgramMode.ToneChase || _senders.Length == 0) return;
            if (_chaseClock.Elapsed.TotalSeconds < _settings.ChaseSeconds) return;
            _chaseIndex = AudioProgramPlanner.NextChasePresenter(_chaseIndex + 1, _senders.Length) - 1;
            _chaseClock.Restart();
            ApplyAudioModes();
        }
    }

    private void ApplyAudioModes()
    {
        foreach (var sender in _senders)
        {
            var mode = AudioProgramPlanner.Resolve(
                _programMode,
                sender.Number,
                _chaseIndex + 1,
                _settings.SourceAudioModes);
            sender.SetAudioMode(mode);
        }
    }

    public void Dispose() => Stop();
}
