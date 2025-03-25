// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestPlatform.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Installer.Tests;

public class LinuxInstallerTests : IDisposable
{
    private readonly DockerHelper _dockerHelper;
    private readonly string _tmpDir;
    private readonly string _contextDir;
    private readonly ITestOutputHelper _outputHelper;
    private readonly string _excludeLinuxArch;

    private bool _rpmContextInitialized = false;
    private bool _debContextInitialized = false;
    private bool _sharedContextInitialized = false;

    private const string NetStandard21RpmPackage = @"https://dotnetcli.blob.core.windows.net/dotnet/Runtime/3.1.0/netstandard-targeting-pack-2.1.0-x64.rpm";
    private const string NetStandard21DebPackage = @"https://dotnetcli.blob.core.windows.net/dotnet/Runtime/3.1.0/netstandard-targeting-pack-2.1.0-x64.deb";
    private const string RuntimeDepsRepo = "mcr.microsoft.com/dotnet/runtime-deps";
    private const string RuntimeDepsVersion = "10.0-preview";

    public static bool IncludeRpmTests => Config.TestRpmPackages;
    public static bool IncludeDebTests => Config.TestDebPackages;

    private enum PackageType
    {
        Rpm,
        Deb
    }

    public LinuxInstallerTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _dockerHelper = new DockerHelper(_outputHelper);

        _tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tmpDir);
        _contextDir = Path.Combine(_tmpDir, Path.GetRandomFileName());
        Directory.CreateDirectory(_contextDir);

        _excludeLinuxArch = Config.Architecture == Architecture.X64 ?
                                                   Architecture.Arm64.ToString().ToLower() :
                                                   Architecture.X64.ToString().ToLower();
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tmpDir, recursive: true);
        }
        catch
        {
        }
    }

    [ConditionalTheory(typeof(LinuxInstallerTests), nameof(IncludeRpmTests))]
    [InlineData(RuntimeDepsRepo, $"{RuntimeDepsVersion}-azurelinux3.0")]
    public void RpmTest(string repo, string tag)
    {
        if (!tag.Contains("azurelinux"))
        {
            // Only Azure Linux is currently supported for RPM tests
            Assert.Fail("Only Azure Linux is currently supported for RPM tests");
        }

        InitializeContext(PackageType.Rpm);

        DistroTest($"{repo}:{tag}", PackageType.Rpm);
    }

    [ConditionalTheory(typeof(LinuxInstallerTests), nameof(IncludeDebTests))]
    [InlineData(RuntimeDepsRepo, $"{RuntimeDepsVersion}-trixie-slim")]
    public void DebTest(string repo, string tag)
    {
        InitializeContext(PackageType.Deb);

        DistroTest($"{repo}:{tag}", PackageType.Deb);
    }

    private void InitializeContext(PackageType packageType)
    {
        string packageArchitecture =
            Config.Architecture == Architecture.X64 ?
                "x64" :
                packageType == PackageType.Rpm ?
                    "aarch64" :
                    "arm64";

        if (packageType == PackageType.Rpm && !_rpmContextInitialized)
        {
            // Copy all applicable RPM packages, excluding Mariner and Azure Linux copies
            List<string> rpmPackages =
                Directory.GetFiles(Config.AssetsDirectory, $"*-{packageArchitecture}*.rpm", SearchOption.AllDirectories)
                .Where(p => !Path.GetFileName(p).Contains("-cm.") &&
                            !Path.GetFileName(p).Contains("-azl-") &&
                            !Path.GetFileName(p).EndsWith("azl.rpm"))
                .ToList();

            foreach (string rpmPackage in rpmPackages)
            {
                File.Copy(rpmPackage, Path.Combine(_contextDir, Path.GetFileName(rpmPackage)));
            }

            if (Config.Architecture == Architecture.X64)
            {
                DownloadFileAsync(NetStandard21RpmPackage, Path.Combine(_contextDir, Path.GetFileName(NetStandard21RpmPackage))).Wait();
            }
            _rpmContextInitialized = true;
        }
        else if (!_debContextInitialized)
        {
            // Copy all applicable DEB packages
            foreach (string debPackage in Directory.GetFiles(Config.AssetsDirectory, $"*-{packageArchitecture}*.deb", SearchOption.AllDirectories))
            {
                File.Copy(debPackage, Path.Combine(_contextDir, Path.GetFileName(debPackage)));
            }

            if (Config.Architecture == Architecture.X64)
            {
                DownloadFileAsync(NetStandard21DebPackage, Path.Combine(_contextDir, Path.GetFileName(NetStandard21DebPackage))).Wait();
            }
            _debContextInitialized = true;
        }

        if (!_sharedContextInitialized)
        {
            // Copy nuget packages
            string nugetPackagesDir = Path.Combine(_contextDir, "packages");
            Directory.CreateDirectory(nugetPackagesDir);
            foreach (string package in Directory.GetFiles(Config.PackagesDirectory, "*.nupkg", SearchOption.AllDirectories))
            {
                if (ShouldCopyPackage(package.ToLower()))
                {
                    File.Copy(package, Path.Combine(nugetPackagesDir, Path.GetFileName(package)));
                }
            }

            // Copy and update NuGet.config from scenario-tests repo
            string newNuGetConfig = Path.Combine(_contextDir, "NuGet.config");
            File.Copy(Config.ScenarioTestsNuGetConfigPath, newNuGetConfig);
            InsertLocalPackagesPathToNuGetConfig(newNuGetConfig, "/packages");

            // Find the scenario-tests package and unpack it to the context dir, subfolder "scenario-tests"
            string? scenarioTestsPackage = Directory.GetFiles(nugetPackagesDir, "Microsoft.DotNet.ScenarioTests.SdkTemplateTests*.nupkg", SearchOption.AllDirectories).FirstOrDefault();
            if (scenarioTestsPackage == null)
            {
                Assert.Fail("Scenario tests package not found");
            }

            ZipFile.ExtractToDirectory(scenarioTestsPackage, Path.Combine(_contextDir, "scenario-tests"));
            _sharedContextInitialized = true;
        }
    }

    private bool ShouldCopyPackage(string package)
    {
        if (package.Contains(".osx-") ||
            package.Contains(".win-") ||
            package.Contains(".linux-musl-") ||
            package.Contains(".linux-bionic-") ||
            package.Contains(".mono.") ||
            package.Contains("symbols") ||
            package.Contains("vs.redist") ||
            package.Contains(".linux-arm.") ||
            package.Contains($".linux-{_excludeLinuxArch}."))
        {
            return false;
        }

        return true;
    }

    private void InsertLocalPackagesPathToNuGetConfig(string nuGetConfig, string localPackagesPath)
    {
        XDocument doc = XDocument.Load(nuGetConfig);
        if (doc.Root != null)
        {
            XElement? packageSourcesElement = doc.Root.Element("packageSources");
            if (packageSourcesElement != null)
            {
                XElement? clearElement = packageSourcesElement.Element("clear");
                if (clearElement != null)
                {
                    XElement newAddElement = new XElement("add",
                        new XAttribute("key", "local-packages"),
                        new XAttribute("value", localPackagesPath));

                    clearElement.AddAfterSelf(newAddElement);
                }
            }

            doc.Save(nuGetConfig);
        }
    }

    private void DistroTest(string baseImage, PackageType packageType)
    {
        List<string> packageList = GetPackageList(baseImage, packageType);
        string dockerfile = GenerateDockerfile(packageList, baseImage, packageType);
        string testCommand = $"dotnet {GetScenarioTestsBinaryPath()} --dotnet-root /usr/share/dotnet/";

        string tag = $"test-{Path.GetRandomFileName()}";
        string output = "";
        bool buildCompleted = false;

        try
        {
            // Build docker image and run the tests
            _dockerHelper.Build(tag, dockerfile: dockerfile, contextDir: _contextDir);
            buildCompleted = true;
            output = _dockerHelper.Run(tag, tag, testCommand);

            int testResultsSummaryIndex = output.IndexOf("Tests run: ");
            if (testResultsSummaryIndex >= 0)
            {
                string testResultsSummary = output[testResultsSummaryIndex..];
                Assert.False(AnyTestFailures(testResultsSummary), testResultsSummary);
            }
            else
            {
                Assert.Fail("Test summary not found");
            }
        }
        catch (Exception e)
        {
            if (string.IsNullOrEmpty(output))
            {
                output = e.Message;
            }
            Assert.Fail($"{(buildCompleted ? "Build" : "Test")} failed: {output}");
        }
        finally
        {
            if (!Config.KeepDockerImages)
            {
                _dockerHelper.DeleteImage(tag);
            }
        }
    }

    private string GetScenarioTestsBinaryPath()
    {
        // Find scenario-tests binary in context/scenario-tests
        string? scenarioTestsBinary = Directory.GetFiles(Path.Combine(_contextDir, "scenario-tests"), "Microsoft.DotNet.ScenarioTests.SdkTemplateTests.dll", SearchOption.AllDirectories).FirstOrDefault();
        if (scenarioTestsBinary == null)
        {
            throw new Exception("Scenario tests binary not found");
        }

        return scenarioTestsBinary.Replace(_contextDir, "").Replace("\\", "/");
    }

    private List<string> GetPackageList(string baseImage, PackageType packageType)
    {
        // Order of installation is important as we do not want to use "--nodeps"
        // We install in correct order, so package dependencies are present.

        // Prepare the package list in correct install order
        List<string> packageList =
        [
            // Deps package should be installed first
            Path.GetFileName(GetMatchingDepsPackage(baseImage, packageType))
        ];

        // Add all other packages in correct install order
        AddPackage(packageList, "dotnet-host-", packageType);
        AddPackage(packageList, "dotnet-hostfxr-", packageType);
        AddPackage(packageList, "dotnet-runtime-", packageType);
        AddPackage(packageList, "dotnet-targeting-pack-", packageType);
        AddPackage(packageList, "aspnetcore-runtime-", packageType);
        AddPackage(packageList, "aspnetcore-targeting-pack-", packageType);
        AddPackage(packageList, "dotnet-apphost-pack-", packageType);
        if (Config.Architecture == Architecture.X64)
        {
            // netstandard package exists for x64 only
            AddPackage(packageList, "netstandard-targeting-pack-", packageType);
        }
        AddPackage(packageList, "dotnet-sdk-", packageType);

        return packageList;
    }

    private string GenerateDockerfile(List<string> packageList, string baseImage, PackageType packageType)
    {
        StringBuilder sb = new();
        sb.AppendLine("FROM " + baseImage);
        sb.AppendLine("");
        sb.AppendLine("# Copy NuGet.config");
        sb.AppendLine($"COPY NuGet.config .");

        sb.AppendLine("");
        sb.AppendLine("# Copy scenario-tests content");
        sb.AppendLine($"COPY scenario-tests scenario-tests");

        sb.AppendLine("");
        sb.AppendLine("# Copy nuget packages");
        sb.AppendLine($"COPY packages packages");

        sb.AppendLine("");
        sb.AppendLine("# Copy installer packages");
        foreach (string package in packageList)
        {
            sb.AppendLine($"COPY {package} {package}");
        }
        sb.AppendLine("");
        sb.AppendLine("# Install the installer packages and Microsoft.DotNet.ScenarioTests.SdkTemplateTests tool");
        sb.Append("RUN");

        // TODO: remove --force-all after deps image issue has been resolved - https://github.com/dotnet/dotnet-docker/issues/6271
        string packageInstallationCommand = packageType == PackageType.Deb ? "dpkg -i --force-all" : "rpm -i";
        bool useAndOperator = false;
        foreach (string package in packageList)
        {
            sb.AppendLine(" \\");
            sb.Append($"    {(useAndOperator ? "&&" : "")} {packageInstallationCommand} {package}");
            useAndOperator = true;
        }
        sb.AppendLine("");

        // Set environment for nuget.config
        sb.AppendLine("");
        sb.AppendLine("# Set custom nuget.config");
        sb.AppendLine("ENV RestoreConfigFile=/NuGet.config");

        string dockerfile = Path.Combine(_contextDir, $"Dockerfile-{Path.GetRandomFileName()}");
        File.WriteAllText(dockerfile, sb.ToString());
        return dockerfile;
    }

    private bool AnyTestFailures(string testResultSummary)
    {
        var parts = testResultSummary.Split(',')
            .Select(part => part.Split(':').Select(p => p.Trim()).ToArray())
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => int.Parse(p[1]));

        return parts["Errors"] > 0 || parts["Failures"] > 0;
    }

    private void AddPackage(List<string> packageList, string prefix, PackageType packageType)
    {
        packageList.Add(Path.GetFileName(GetContentPackage(prefix, packageType)));
    }

    private string GetContentPackage(string prefix, PackageType packageType)
    {
        string matchPattern = PackageType.Deb == packageType ? "*.deb" : "*.rpm";
        string[] files = Directory.GetFiles(_contextDir, prefix + matchPattern, SearchOption.AllDirectories)
            .Where(p => !Path.GetFileName(p).Contains("dotnet-runtime-deps-"))
            .ToArray();
        if (files.Length == 0)
        {
            throw new Exception($"RPM package with prefix '{prefix}' not found");
        }

        return files.OrderByDescending(f => f).First();
    }

    private string GetMatchingDepsPackage(string baseImage, PackageType packageType)
    {
        string matchPattern = packageType == PackageType.Deb
            ? "dotnet-runtime-deps-*.deb"
            : "dotnet-runtime-deps-*azl*.rpm"; // We currently only support Azure Linux deps image

        string[] files = Directory.GetFiles(_contextDir, matchPattern, SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            throw new Exception($"Did not find the DEPS package.");
        }

        return files.OrderByDescending(f => f).First();
    }

    private static async Task DownloadFileAsync(string url, string filePath)
    {
        using (HttpClient client = new HttpClient())
        {
            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await response.Content.CopyToAsync(fileStream);
            }
        }
    }
}

