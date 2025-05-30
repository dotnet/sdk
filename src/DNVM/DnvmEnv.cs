
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serde.Json;
using Zio;
using Zio.FileSystems;
using static System.Environment;

namespace Microsoft.DotNet.DNVM;

/// <summary>
/// Represents the environment of a dnvm process.
/// <summary>
public sealed partial class DnvmEnv
{
    public bool IsPhysicalDnvmHome { get; }
    public readonly IFileSystem DnvmHomeFs;
    public readonly IFileSystem CwdFs;
    public readonly UPath Cwd;
    public string RealPath(UPath path) => DnvmHomeFs.ConvertPathToInternal(path);
    public SubFileSystem TempFs { get; }
    public Func<string, string?> GetUserEnvVar { get; }
    public Action<string, string> SetUserEnvVar { get; }
    public IEnumerable<string> DotnetFeedUrls { get; }
    public string UserHome { get; }
    public ScopedHttpClient HttpClient { get; }

    public DnvmEnv(
        string userHome,
        IFileSystem homeFs,
        IFileSystem cwdFs,
        UPath cwd,
        bool isPhysical,
        Func<string, string?> getUserEnvVar,
        Action<string, string> setUserEnvVar,
        IEnumerable<string>? dotnetFeedUrls = null,
        HttpClient? httpClient = null)
    {
        UserHome = userHome;
        DnvmHomeFs = homeFs;
        CwdFs = cwdFs;
        Cwd = cwd;
        IsPhysicalDnvmHome = isPhysical;
        // TempFs must be a physical file system because we pass the path to external
        // commands that will not be able to write to shared memory
        TempFs = new SubFileSystem(
            PhysicalFs,
            PhysicalFs.ConvertPathFromInternal(Path.GetTempPath()),
            owned: false);
        GetUserEnvVar = getUserEnvVar;
        SetUserEnvVar = setUserEnvVar;
        DotnetFeedUrls = dotnetFeedUrls ?? DefaultDotnetFeedUrls;
        HttpClient = new ScopedHttpClient(httpClient ?? new HttpClient() {
            Timeout = Timeout.InfiniteTimeSpan
        });
    }
}

public sealed partial class DnvmEnv : IDisposable
{
    public const string ManifestFileName = "dnvmManifest.json";
    public static EqArray<string> DefaultDotnetFeedUrls { get;} = [
        "https://builds.dotnet.microsoft.com/dotnet",
        "https://dotnetcli.blob.core.windows.net/dotnet",
    ];
    public static UPath ManifestPath => UPath.Root / ManifestFileName;
    public static UPath EnvPath => UPath.Root / "env";
    public static UPath DnvmExePath => UPath.Root / Utilities.DnvmExeName;
    public static UPath SymlinkPath => UPath.Root / Utilities.DotnetExeName;
    public static UPath GetSdkPath(SdkDirName sdkDirName) => UPath.Root / sdkDirName.Name;
    /// <summary>
    /// Default DNVM_HOME is
    ///  ~/.local/share/dnvm on Linux
    ///  %LocalAppData%/dnvm on Windows
    ///  ~/Library/Application Support/dnvm on Mac
    /// </summary>
    public static readonly string DefaultDnvmHome = Path.Combine(
        GetFolderPath(SpecialFolder.LocalApplicationData, SpecialFolderOption.DoNotVerify),
        "dnvm");
    /// <summary>
    /// The location of the SDK install directory, relative to <see cref="DnvmHome" />
    /// </summary>
    public static readonly SdkDirName DefaultSdkDirName = new("dn");
    public static readonly PhysicalFileSystem PhysicalFs = new();
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Get the path to DNVM_HOME, which is the location of the dnvm manifest
    /// and the installed SDKs. If the environment variable is not set, uses
    /// <see cref="DnvmEnv.DefaultDnvmHome" /> as the default.
    /// </summar>
    public static DnvmEnv CreateDefault(
        string? home = null,
        string? dotnetFeedUrl = null)
    {
        home ??= Environment.GetEnvironmentVariable("DNVM_HOME");
        var dnvmHome = string.IsNullOrWhiteSpace(home)
            ? DefaultDnvmHome
            : home;
        return CreatePhysical(dnvmHome,
            n => Environment.GetEnvironmentVariable(n, EnvironmentVariableTarget.User),
            (n, v) => Environment.SetEnvironmentVariable(n, v, EnvironmentVariableTarget.User),
            dotnetFeedUrl);
    }

    public static DnvmEnv CreatePhysical(
        string realPath,
        Func<string, string?> getUserEnvVar,
        Action<string, string> setUserEnvVar,
        string? dotnetFeedUrl = null)
    {
        Directory.CreateDirectory(realPath);

        return new DnvmEnv(
            userHome: GetFolderPath(SpecialFolder.UserProfile, SpecialFolderOption.DoNotVerify),
            new SubFileSystem(PhysicalFs, PhysicalFs.ConvertPathFromInternal(realPath)),
            PhysicalFs,
            PhysicalFs.ConvertPathFromInternal(Environment.CurrentDirectory),
            isPhysical: true,
            getUserEnvVar,
            setUserEnvVar,
            dotnetFeedUrls: dotnetFeedUrl is not null ? [ dotnetFeedUrl ] : null);
    }

    /// <summary>
    /// Reads a manifest (any version) from the given path and returns an up-to-date <see
    /// cref="Manifest" /> (latest version).  Throws if the manifest is invalid.
    /// </summary>
    public async Task<Manifest> ReadManifest()
    {
        var text = DnvmHomeFs.ReadAllText(ManifestPath);
        return await ManifestUtils.DeserializeNewOrOldManifest(HttpClient, text, DotnetFeedUrls);
    }

    public void WriteManifest(Manifest manifest)
    {
        var text = JsonSerializer.Serialize(manifest);
        DnvmHomeFs.WriteAllText(ManifestPath, text, Encoding.UTF8);
    }

    public void Dispose()
    { }
}
