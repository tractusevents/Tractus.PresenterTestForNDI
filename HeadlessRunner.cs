using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Tractus.PresenterTest;

public static class HeadlessRunner
{
    public static async Task<int> RunAsync(string[] args)
    {
        ConsoleHost.Attach();
        try
        {
            var options = HeadlessOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            if (options.PreviewDirectory is not null)
            {
                Directory.CreateDirectory(options.PreviewDirectory);
                for (var number = 1; number <= options.Settings.SourceCount; number++)
                {
                    var pixels = PresenterImageLoader.LoadBgra(number, VideoSpec.Width, VideoSpec.Height);
                    var path = Path.Combine(options.PreviewDirectory, $"presenter-{number}.bmp");
                    PresenterImageLoader.WritePreviewBitmap(path, pixels, VideoSpec.Width, VideoSpec.Height);
                    Console.WriteLine($"Wrote {path}");
                }
                return 0;
            }

            using var engine = new PresenterEngine(options.Settings);
            using var stop = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                stop.Cancel();
            };

            engine.Start();
            engine.SetProgramMode(options.Settings.AudioProgram);
            Console.WriteLine($"Tractus Presenter Test for NDI — {options.Settings.SourceCount} sources at 1920×1080p30");
            Console.WriteLine($"Audio program: {options.Settings.AudioProgram}");
            Console.WriteLine("Press Ctrl+C to stop.");

            var clock = Stopwatch.StartNew();
            while (!stop.IsCancellationRequested)
            {
                await Task.Delay(250, stop.Token).ConfigureAwait(false);
                if (engine.Senders.FirstOrDefault(sender => sender.Failure is not null) is { } failed)
                    throw new InvalidOperationException($"{failed.SourceName} failed", failed.Failure);
                if (clock.Elapsed >= TimeSpan.FromSeconds(5))
                {
                    var rates = engine.Senders.Select(sender => sender.MeasuredFps).ToArray();
                    var receivers = engine.Senders.Sum(sender => sender.Connections);
                    Console.WriteLine($"Live — {rates.Min():0.0}-{rates.Max():0.0} fps, {receivers} connected receiver(s)" +
                                      (engine.ChasePresenterNumber > 0 ? $", tone on presenter {engine.ChasePresenterNumber}" : string.Empty));
                    clock.Restart();
                }
            }
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.GetBaseException().Message}");
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Tractus Presenter Test for NDI");
        Console.WriteLine();
        Console.WriteLine("GUI: TractusPresenterTestForNDI.exe");
        Console.WriteLine("Headless: TractusPresenterTestForNDI.exe --headless [options]");
        Console.WriteLine();
        Console.WriteLine("  --count 1-8");
        Console.WriteLine("  --name-prefix NAME");
        Console.WriteLine("  --audio individual|tone|silence|none|chase");
        Console.WriteLine("  --tone-level-db -60..-3");
        Console.WriteLine("  --chase-seconds 0.5..30");
        Console.WriteLine("  --preview DIRECTORY");
    }

    private sealed record HeadlessOptions(AppSettings Settings, string? PreviewDirectory, bool ShowHelp)
    {
        public static HeadlessOptions Parse(string[] args)
        {
            var settings = new AppSettings();
            string? preview = null;
            var help = false;
            var noAudio = false;
            for (var index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--headless": break;
                    case "--count": settings.SourceCount = int.Parse(args[++index], CultureInfo.InvariantCulture); break;
                    case "--name-prefix": settings.NamePrefix = args[++index]; break;
                    case "--tone-level-db": settings.ToneLevelDb = double.Parse(args[++index], CultureInfo.InvariantCulture); break;
                    case "--chase-seconds": settings.ChaseSeconds = double.Parse(args[++index], CultureInfo.InvariantCulture); break;
                    case "--preview": preview = Path.GetFullPath(args[++index]); break;
                    case "--audio":
                        var audioValue = args[++index];
                        noAudio = audioValue.Equals("none", StringComparison.OrdinalIgnoreCase);
                        settings.AudioProgram = ParseAudio(audioValue);
                        break;
                    case "--help" or "-h" or "/?": help = true; break;
                    default: throw new ArgumentException($"Unknown option: {args[index]}");
                }
            }
            settings.Normalize();
            if (settings.AudioProgram == AudioProgramMode.Individual)
            {
                var mode = noAudio ? AudioMode.NoAudio : AudioMode.Silence;
                settings.SourceAudioModes = Enumerable.Repeat(mode, 8).ToArray();
            }
            return new HeadlessOptions(settings, preview, help);
        }

        private static AudioProgramMode ParseAudio(string value) => value.ToLowerInvariant() switch
        {
            "tone" => AudioProgramMode.AllTones,
            "silence" => AudioProgramMode.SilenceAll,
            "chase" => AudioProgramMode.ToneChase,
            "individual" or "none" => AudioProgramMode.Individual,
            _ => throw new ArgumentException("Audio must be individual, tone, silence, none, or chase.")
        };
    }
}

internal static class ConsoleHost
{
    private const uint AttachParentProcess = 0xffffffff;

    public static void Attach()
    {
        if (!AttachConsole(AttachParentProcess)) AllocConsole();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();
}
