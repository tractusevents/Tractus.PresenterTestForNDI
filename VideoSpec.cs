namespace Tractus.PresenterTest;

public static class VideoSpec
{
    public const int Width = 1920;
    public const int Height = 1080;
    public const int Fps = 30;
    public const int AudioSampleRate = 48000;
    public const int AudioChannels = 2;
    public const int AudioSamplesPerFrame = AudioSampleRate / Fps;
}
