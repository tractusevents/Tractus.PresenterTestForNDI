using Tractus.Ndi;

namespace Tractus.PresenterTest;

public static class NdiRuntime
{
    private static readonly object Gate = new();
    private static bool _initialized;

    public static string? LoadedPath { get; private set; }

    public static void EnsureInitialized()
    {
        lock (Gate)
        {
            if (_initialized) return;
            LoadedPath = LocateBasicRuntime();
            if (LoadedPath is null)
                throw new DllNotFoundException(
                    "Processing.NDI.Lib.x64.dll was not found. Install NDI 6.3 Tools/runtime or place the DLL beside this application.");
            NDIWrapper.Initialize(useAdvancedDynLib: false, exactLibLookupPath: LoadedPath);
            _initialized = true;
        }
    }

    private static string? LocateBasicRuntime()
    {
        const string fileName = "Processing.NDI.Lib.x64.dll";
        var directories = new List<string?>
        {
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable("NDI_RUNTIME_DIR"),
            Environment.GetEnvironmentVariable("NDI_RUNTIME_DIR_V6"),
            Environment.GetEnvironmentVariable("NDI_LIBRARY_PATH"),
            Environment.GetEnvironmentVariable("NDI_SDK_DIR"),
            @"C:\Program Files\NDI\NDI 6 Tools\Runtime",
            @"C:\Program Files\NDI\NDI 6 Tools\Router",
            @"C:\Program Files\NDI\NDI 6 Runtime\v6",
            @"C:\Program Files\NDI\NDI 6 SDK\Bin\x64"
        };

        foreach (var directory in directories.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var direct = Path.Combine(directory!, fileName);
            if (File.Exists(direct)) return direct;
            var bin = Path.Combine(directory!, "bin", fileName);
            if (File.Exists(bin)) return bin;
            var lib = Path.Combine(directory!, "lib", fileName);
            if (File.Exists(lib)) return lib;
        }
        return null;
    }
}
