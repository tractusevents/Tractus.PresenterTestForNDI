namespace Tractus.PresenterTest;

public static class AudioProgramPlanner
{
    public static AudioMode Resolve(
        AudioProgramMode program,
        int presenterNumber,
        int chasePresenterNumber,
        IReadOnlyList<AudioMode> individualModes)
    {
        if (presenterNumber < 1 || presenterNumber > individualModes.Count)
            throw new ArgumentOutOfRangeException(nameof(presenterNumber));

        return program switch
        {
            AudioProgramMode.AllTones => AudioMode.Tone,
            AudioProgramMode.SilenceAll => AudioMode.Silence,
            AudioProgramMode.ToneChase => presenterNumber == chasePresenterNumber
                ? AudioMode.Tone
                : AudioMode.Silence,
            _ => individualModes[presenterNumber - 1]
        };
    }

    public static int NextChasePresenter(int currentPresenterNumber, int presenterCount)
    {
        if (presenterCount < 1) throw new ArgumentOutOfRangeException(nameof(presenterCount));
        return currentPresenterNumber >= presenterCount ? 1 : currentPresenterNumber + 1;
    }
}
