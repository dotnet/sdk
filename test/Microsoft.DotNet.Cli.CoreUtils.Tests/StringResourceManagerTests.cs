// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;

namespace Microsoft.DotNet.Cli.CoreUtils.Tests;

[TestClass]
public sealed class StringResourceManagerTests
{
    private static readonly Assembly s_testAssembly = ResourceTestUtilities.TestAssembly;

    public TestContext TestContext { get; set; } = null!;

    private string TestRunDirectory => TestContext.TestRunDirectory
        ?? throw new InvalidOperationException("The test run directory is unavailable.");

    [TestMethod]
    public void FromAssemblyFile_ReadsEmbeddedResourceWithoutLoadingAnotherAssembly()
    {
        StringResourceManager manager = StringResourceManager.FromAssemblyFile(
            ResourceTestUtilities.NeutralBaseName,
            s_testAssembly.Location);

        Assert.AreEqual("Hello", manager.GetString("Greeting"));
    }

    [TestMethod]
    public void FromAssemblyFile_MalformedPortableExecutable_ThrowsBadImageFormatException()
    {
        using TestDirectory directory = new(TestRunDirectory);
        string assemblyFile = Path.Combine(directory.Path, "Malformed.dll");
        File.WriteAllBytes(assemblyFile, [0x00, 0x01, 0x02, 0x03]);
        StringResourceManager manager = StringResourceManager.FromAssemblyFile(
            ResourceTestUtilities.NeutralBaseName,
            assemblyFile);

        Assert.ThrowsExactly<BadImageFormatException>(() => manager.GetString("Greeting"));
    }

    [TestMethod]
    public void FromResourcesDirectory_UsesExactThenParentThenNeutralFallback()
    {
        using TestDirectory directory = new(TestRunDirectory);
        const string baseName = "Strings";
        ResourceTestUtilities.WriteResources(
            directory.Path,
            "fr",
            baseName,
            ("ParentOnly", "Parent"),
            ("Overridden", "Parent"));
        ResourceTestUtilities.WriteResources(
            directory.Path,
            "fr-CA",
            baseName,
            ("ExactOnly", "Exact"),
            ("Overridden", "Exact"));

        byte[] neutralBytes = ResourceTestUtilities.WriteResources(("NeutralOnly", "Neutral"));
        StringResourceManager neutral = new(
            baseName,
            () => new MemoryStream(neutralBytes, writable: false));
        SatelliteStringResourceManager manager =
            SatelliteStringResourceManager.FromResourcesDirectory(
                baseName,
                directory.Path,
                neutral,
                StringResourceManagerOptions.None);

        CultureInfo culture = CultureInfo.GetCultureInfo("fr-CA");
        Assert.AreEqual("Exact", manager.GetString("ExactOnly", culture));
        Assert.AreEqual("Exact", manager.GetString("Overridden", culture));
        Assert.AreEqual("Parent", manager.GetString("ParentOnly", culture));
        Assert.AreEqual("Neutral", manager.GetString("NeutralOnly", culture));
    }

    [TestMethod]
    public void ValidSatellite_ReadsLocalizedStringsAndFallsBackToNeutral()
    {
        string assemblyDirectory = Path.GetDirectoryName(s_testAssembly.Location)
            ?? throw new InvalidOperationException("The test assembly has no directory.");
        string baseName = ResourceTestUtilities.NeutralBaseName;

        SatelliteStringResourceManager fromSatelliteDirectory =
            SatelliteStringResourceManager.FromSatelliteDirectory(
                baseName,
                assemblyDirectory,
                s_testAssembly,
                SatelliteStringResourceProbeMode.Strict);
        SatelliteStringResourceManager fromAssemblyFiles =
            SatelliteStringResourceManager.FromAssemblyFiles(
                baseName,
                s_testAssembly.Location,
                assemblyDirectory,
                SatelliteStringResourceProbeMode.Strict);

        CultureInfo french = CultureInfo.GetCultureInfo("fr");
        CultureInfo frenchCanadian = CultureInfo.GetCultureInfo("fr-CA");
        Assert.AreEqual("Bonjour", fromSatelliteDirectory.GetString("Greeting", french));
        Assert.AreEqual("Bonjour", fromSatelliteDirectory.GetString("Greeting", frenchCanadian));
        Assert.AreEqual("Neutral", fromSatelliteDirectory.GetString("NeutralOnly", frenchCanadian));
        Assert.AreEqual("Bonjour", fromAssemblyFiles.GetString("Greeting", french));
        Assert.AreEqual("Bonjour", fromAssemblyFiles.GetString("Greeting", frenchCanadian));
        Assert.AreEqual("Neutral", fromAssemblyFiles.GetString("NeutralOnly", frenchCanadian));
    }

    [TestMethod]
    public void FromSatelliteDirectory_FallbackOnFailure_SkipsMalformedCandidate()
    {
        using TestDirectory directory = new(TestRunDirectory);
        string cultureDirectory = Path.Combine(directory.Path, "de-DE");
        Directory.CreateDirectory(cultureDirectory);
        string assemblyName = s_testAssembly.GetName().Name
            ?? throw new InvalidOperationException("The test assembly has no simple name.");
        File.WriteAllBytes(
            Path.Combine(cultureDirectory, $"{assemblyName}.resources.dll"),
            [0x00, 0x01, 0x02, 0x03]);

        SatelliteStringResourceManager manager =
            SatelliteStringResourceManager.FromSatelliteDirectory(
                ResourceTestUtilities.NeutralBaseName,
                directory.Path,
                s_testAssembly,
                SatelliteStringResourceProbeMode.FallbackOnFailure);

        Assert.AreEqual(
            "Hello",
            manager.GetString("Greeting", CultureInfo.GetCultureInfo("de-DE")));
    }

    [TestMethod]
    public void FromAssemblyFiles_AliasedOwnerValidatesIdentityAndReadsNeutralResource()
    {
        using TestDirectory directory = new(TestRunDirectory);
        string simpleName = s_testAssembly.GetName().Name
            ?? throw new InvalidOperationException("The test assembly has no simple name.");

        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromAssemblyFiles(
            ResourceTestUtilities.NeutralBaseName,
            s_testAssembly.Location,
            directory.Path,
            s_testAssembly,
            simpleName,
            SatelliteStringResourceProbeMode.FallbackOnFailure);

        Assert.AreEqual("Hello", manager.GetString("Greeting", CultureInfo.InvariantCulture));
        Assert.ThrowsExactly<FileLoadException>(
            () => SatelliteStringResourceManager.FromAssemblyFiles(
                ResourceTestUtilities.NeutralBaseName,
                s_testAssembly.Location,
                directory.Path,
                s_testAssembly,
                "WrongOwner",
                SatelliteStringResourceProbeMode.FallbackOnFailure));
    }

    [TestMethod]
    public async Task GetString_ConcurrentFirstLookup_InvokesFactoryOnce()
    {
        byte[] resources = ResourceTestUtilities.WriteResources(("Greeting", "Hello"));
        using ManualResetEventSlim loadStarted = new();
        using ManualResetEventSlim continueLoad = new();
        CancellationToken cancellationToken = TestContext.CancellationToken;
        int invocationCount = 0;
        StringResourceManager manager = new("Strings", CreateStream);

        Stream CreateStream()
        {
            Interlocked.Increment(ref invocationCount);
            loadStarted.Set();
            continueLoad.Wait(cancellationToken);
            return new MemoryStream(resources, writable: false);
        }

        Task<string?> first = Task.Run(() => manager.GetString("Greeting"), cancellationToken);
        Assert.IsTrue(loadStarted.Wait(TimeSpan.FromSeconds(10), cancellationToken));
        Task<string?> second = Task.Run(() => manager.GetString("Greeting"), cancellationToken);
        continueLoad.Set();

        string?[] values = await Task.WhenAll(first, second);
        Assert.AreSequenceEqual(new[] { "Hello", "Hello" }, values);
        Assert.AreEqual(1, invocationCount);
    }
}
