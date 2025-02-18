// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestPlatform.Utilities;
using Newtonsoft.Json.Linq;
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
using Xunit;
using Xunit.Abstractions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Microsoft.DotNet.Installer.Tests;

public class LinuxInstallerTests : IDisposable
{
    private readonly DockerHelper _dockerHelper;
    private readonly string _tmpDir;
    private readonly string _contextDir;

    private readonly string[] RpmDistroImages =
    [
        "mcr.microsoft.com/dotnet-buildtools/prereqs:fedora-40"
    ];

    private readonly string[] DebDistroImages =
    [
        "mcr.microsoft.com/dotnet-buildtools/prereqs:ubuntu-24.04"
    ];

    private const string NetStandard21RpmPackage = @"https://dotnetcli.blob.core.windows.net/dotnet/Runtime/3.1.0/netstandard-targeting-pack-2.1.0-x64.rpm";
    private const string NetStandard21DebPackage = @"https://dotnetcli.blob.core.windows.net/dotnet/Runtime/3.1.0/netstandard-targeting-pack-2.1.0-x64.deb";

    private enum DistroType
    {
        Rpm,
        Deb
    }

    private ITestOutputHelper OutputHelper { get; set; }

    public LinuxInstallerTests(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
        _dockerHelper = new DockerHelper(OutputHelper);

        _tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tmpDir);
        _contextDir = Path.Combine(_tmpDir, Path.GetRandomFileName());
        Directory.CreateDirectory(_contextDir);

        InitializeContext();
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

    [Fact]
    public void RunScenarioTestsForAllDistros()
    {
        if (Config.TestRpmPackages)
        {
            TestAllDistros(DistroType.Rpm);
        }


        if (Config.TestDebPackages)
        {
            TestAllDistros(DistroType.Deb);
        }
    }

    private void TestAllDistros(DistroType distroType)
    {
        foreach (string image in (distroType == DistroType.Rpm ? RpmDistroImages : DebDistroImages))
        {
            try
            {
                OutputHelper.WriteLine($"Begin testing distro: {image}");
                DistroTest(image, distroType);
                OutputHelper.WriteLine($"Finished testing distro: {image}");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Test failed for {image}: {ex}");
            }
        }
    }

    private void InitializeContext()
    {
        // For rpm enumerate RPM packages, excluding those that contain ".cm." in the name
        List<string> rpmPackages = Directory.GetFiles(Config.AssetsDirectory, "*.rpm", SearchOption.AllDirectories)
            .Where(p => !Path.GetFileName(p).Contains("-cm.") && !Path.GetFileName(p).EndsWith("azl.rpm"))
            .ToList();

        foreach (string rpmPackage in rpmPackages)
        {
            File.Copy(rpmPackage, Path.Combine(_contextDir, Path.GetFileName(rpmPackage)));
        }

        // Copy all DEB packages as well
        foreach (string debPackage in Directory.GetFiles(Config.AssetsDirectory, "*.deb", SearchOption.AllDirectories))
        {
            File.Copy(debPackage, Path.Combine(_contextDir, Path.GetFileName(debPackage)));
        }

        // Download NetStandard 2.1 packages
        DownloadFileAsync(NetStandard21RpmPackage, Path.Combine(_contextDir, Path.GetFileName(NetStandard21RpmPackage))).Wait();
        DownloadFileAsync(NetStandard21DebPackage, Path.Combine(_contextDir, Path.GetFileName(NetStandard21DebPackage))).Wait();

        // Copy nuget packages
        string nugetPackagesDir = Path.Combine(_contextDir, "packages");
        Directory.CreateDirectory(nugetPackagesDir);
        foreach (string package in Directory.GetFiles(Config.PackagesDirectory, "*.nupkg", SearchOption.AllDirectories))
        {
            File.Copy(package, Path.Combine(nugetPackagesDir, Path.GetFileName(package)));
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

    private void DistroTest(string baseImage, DistroType distroType)
    {
        // Order of installation is important as we do not want to use "--nodeps"
        // We install in correct order, so package dependencies are present.

        // Prepare the package list in correct install order
        List<string> packageList =
        [
            // Deps package should be installed first
            Path.GetFileName(GetMatchingDepsPackage(baseImage, distroType))
        ];

        // Add all other packages in correct install order
        AddPackage(packageList, "dotnet-host-", distroType);
        AddPackage(packageList, "dotnet-hostfxr-", distroType);
        AddPackage(packageList, "dotnet-runtime-", distroType);
        AddPackage(packageList, "dotnet-targeting-pack-", distroType);
        AddPackage(packageList, "aspnetcore-runtime-", distroType);
        AddPackage(packageList, "aspnetcore-targeting-pack-", distroType);
        AddPackage(packageList, "dotnet-apphost-pack-", distroType);
        if (Config.Architecture == "x64")
        {
            // netstandard package exists for x64 only
            AddPackage(packageList, "netstandard-targeting-pack-", distroType);
        }
        AddPackage(packageList, "dotnet-sdk-", distroType);

        string dockerfile = GenerateDockerfile(packageList, baseImage, distroType);

        string tag = $"test-{Path.GetRandomFileName()}";
        string output = "";

        try
        {
            // Build docker image and run the tests
            _dockerHelper.Build(tag, dockerfile: dockerfile, contextDir: _contextDir);
            output = _dockerHelper.Run(tag, tag);

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
            Assert.Fail($"Build failed: {output}");
        }
        finally
        {
            _dockerHelper.DeleteImage(tag);
        }
    }

    private string GenerateDockerfile(List<string> rpmPackageList, string baseImage, DistroType distroType)
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
        sb.AppendLine("# Copy RPM packages");
        foreach (string package in rpmPackageList)
        {
            sb.AppendLine($"COPY {package} {package}");
        }
        sb.AppendLine("");
        sb.AppendLine("# Install RPM packages and Microsoft.DotNet.ScenarioTests.SdkTemplateTests tool");
        sb.Append("RUN");

        // TODO: remove --force-all when aspnet package versioning issue have been resolved - https://github.com/dotnet/source-build/issues/4895
        string packageInstallationCommand = distroType == DistroType.Deb ? "dpkg -i --force-all" : "rpm -i";
        bool useAndOperator = false;
        foreach (string package in rpmPackageList)
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

        // Find scenario-tests binary in context/scenario-tests
        string? scenarioTestsBinary = Directory.GetFiles(Path.Combine(_contextDir, "scenario-tests"), "Microsoft.DotNet.ScenarioTests.SdkTemplateTests.dll", SearchOption.AllDirectories).FirstOrDefault();
        if (scenarioTestsBinary == null)
        {
            throw new Exception("Scenario tests binary not found");
        }
        scenarioTestsBinary = scenarioTestsBinary.Replace(_contextDir, "").Replace("\\", "/");

        // Set entry point
        sb.AppendLine("");
        sb.AppendLine($"ENTRYPOINT [ \"dotnet\", \"{scenarioTestsBinary}\", \"--dotnet-root\", \"/usr/share/dotnet\" ]");

        string dockerfile = Path.Combine(_contextDir, Path.GetRandomFileName());
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

    private void AddPackage(List<string> packageList, string prefix, DistroType distroType)
    {
        packageList.Add(Path.GetFileName(GetContentPackage(prefix, distroType)));
    }

    private string GetContentPackage(string prefix, DistroType distroType)
    {
        string matchPattern = DistroType.Deb == distroType ? "*.deb" : "*.rpm";
        string[] rpmFiles = Directory.GetFiles(_contextDir, prefix + matchPattern, SearchOption.AllDirectories)
            .Where(p => !Path.GetFileName(p).Contains("dotnet-runtime-deps-"))
            .ToArray();
        if (rpmFiles.Length == 0)
        {
            throw new Exception($"RPM package with prefix '{prefix}' not found");
        }

        return rpmFiles.OrderByDescending(f => f).First();
    }

    private string GetMatchingDepsPackage(string baseImage, DistroType distroType)
    {
        string matchPattern = "dotnet-runtime-deps-*.deb";
        if (distroType == DistroType.Rpm)
        {
            string depsId = "fedora";
            if (baseImage.Contains("fedora"))
            {
                depsId = "fedora";
            }
            else if (baseImage.Contains("centos"))
            {
                depsId = "centos";
            }
            else if (baseImage.Contains("rhel"))
            {
                depsId = "rhel";
            }
            else if (baseImage.Contains("opensuse"))
            {
                depsId = "opensuse";
            }
            else if (baseImage.Contains("sles"))
            {
                depsId = "sles";
            }
            else if (baseImage.Contains("azurelinux"))
            {
                depsId = "azl";
            }
            else
            {
                throw new Exception($"Unknown distro: {baseImage}");
            }

            matchPattern = $"dotnet-runtime-deps-*{depsId}*.rpm";
        }

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

