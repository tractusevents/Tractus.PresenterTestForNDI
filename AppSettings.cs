namespace Tractus.PresenterTest;

public sealed class AppSettings
{
    public int SourceCount { get; set; } = 8;
    public string NamePrefix { get; set; } = "Presenter Test";
    public double ToneLevelDb { get; set; } = -20;
    public double ChaseSeconds { get; set; } = 2;
    public AudioProgramMode AudioProgram { get; set; } = AudioProgramMode.SilenceAll;
    public AudioMode[] SourceAudioModes { get; set; } = Enumerable.Repeat(AudioMode.Silence, 8).ToArray();
    public string?[] CustomImagePaths { get; set; } = new string?[8];

    public void Normalize()
    {
        SourceCount = Math.Clamp(SourceCount, 1, 8);
        NamePrefix = string.IsNullOrWhiteSpace(NamePrefix) ? "Presenter Test" : NamePrefix.Trim();
        ToneLevelDb = Math.Clamp(ToneLevelDb, -60, -3);
        ChaseSeconds = Math.Clamp(ChaseSeconds, 0.5, 30);
        if (SourceAudioModes is not { Length: 8 })
            SourceAudioModes = Enumerable.Repeat(AudioMode.Silence, 8).ToArray();
        if (CustomImagePaths is not { Length: 8 })
            CustomImagePaths = new string?[8];
        for (var index = 0; index < CustomImagePaths.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(CustomImagePaths[index]) || !File.Exists(CustomImagePaths[index]))
                CustomImagePaths[index] = null;
        }
    }
}
