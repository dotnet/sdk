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
    public void GetString_StreamFactoryFailure_RetriesAndCachesSuccessfulLoad()
    {
        byte[] resources = ResourceTestUtilities.WriteResources(("Greeting", "Hello"));
        IOException failure = new("Factory failure.");
        int invocationCount = 0;
        StringResourceManager manager = new(
            "Strings",
            () =>
            {
                if (++invocationCount == 1)
                {
                    throw failure;
                }

                return new MemoryStream(resources, writable: false);
            });

        try
        {
            IOException actual = Assert.ThrowsExactly<IOException>(() => manager.GetString("Greeting"));
            Assert.AreSame(failure, actual);
            Assert.AreEqual("Hello", manager.GetString("Greeting"));
            Assert.AreEqual("Hello", manager.GetString("Greeting"));
            Assert.AreEqual(2, invocationCount);
        }
        finally
        {
            manager.ReleaseAllResources();
        }
    }

    [TestMethod]
    public void GetString_MalformedStream_RetriesAndDisposesEachAttempt()
    {
        MemoryStream? lastStream = null;
        int invocationCount = 0;
        StringResourceManager manager = new(
            "Strings",
            () =>
            {
                invocationCount++;
                lastStream = new(new byte[64], writable: false);
                return lastStream;
            });

        try
        {
            for (int i = 0; i < 2; i++)
            {
                Assert.ThrowsExactly<ArgumentException>(() => manager.GetString("Greeting"));
                Assert.IsNotNull(lastStream);
                Assert.IsFalse(lastStream.CanRead);
            }

            Assert.AreEqual(2, invocationCount);
        }
        finally
        {
            manager.ReleaseAllResources();
        }
    }

    [TestMethod]
    public void GetString_LocalizedFailure_RetriesRepairedSourceWithoutRelease()
    {
        using TestDirectory directory = new(TestRunDirectory);
        const string baseName = "Strings";
        string cultureDirectory = Path.Combine(directory.Path, "fr");
        Directory.CreateDirectory(cultureDirectory);
        File.WriteAllBytes(Path.Combine(cultureDirectory, $"{baseName}.resources"), new byte[64]);
        StringResourceManager neutral = new(
            baseName,
            () => new MemoryStream(
                ResourceTestUtilities.WriteResources(("Greeting", "Hello")),
                writable: false));
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            directory.Path,
            neutral);

        try
        {
            CultureInfo french = CultureInfo.GetCultureInfo("fr");
            Assert.ThrowsExactly<ArgumentException>(() => manager.GetString("Greeting", french));
            ResourceTestUtilities.WriteResources(directory.Path, "fr", baseName, ("Greeting", "Bonjour"));

            Assert.AreEqual("Bonjour", manager.GetString("Greeting", french));
        }
        finally
        {
            manager.ReleaseAllResources();
            neutral.ReleaseAllResources();
        }
    }

    [TestMethod]
    public void GetString_MissingLocalizedSource_RemainsCachedUntilRelease()
    {
        using TestDirectory directory = new(TestRunDirectory);
        const string baseName = "Strings";
        byte[] resources = ResourceTestUtilities.WriteResources(("Greeting", "Hello"));
        StringResourceManager neutral = new(
            baseName,
            () => new MemoryStream(resources, writable: false));
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            directory.Path,
            neutral);

        try
        {
            CultureInfo french = CultureInfo.GetCultureInfo("fr");
            Assert.AreEqual("Hello", manager.GetString("Greeting", french));
            ResourceTestUtilities.WriteResources(directory.Path, "fr", baseName, ("Greeting", "Bonjour"));
            Assert.AreEqual("Hello", manager.GetString("Greeting", french));

            manager.ReleaseAllResources();

            Assert.AreEqual("Bonjour", manager.GetString("Greeting", french));
        }
        finally
        {
            manager.ReleaseAllResources();
            neutral.ReleaseAllResources();
        }
    }

    [TestMethod]
    public void GetString_CallerOwnedEmbeddedManager_ForwardsRequestedCulture()
    {
        using TestDirectory directory = new(TestRunDirectory);
        string baseName = ResourceTestUtilities.NeutralBaseName;
        EmbeddedStringResourceManager neutral = new(baseName, s_testAssembly);
        SatelliteStringResourceManager manager = SatelliteStringResourceManager.FromResourcesDirectory(
            baseName,
            directory.Path,
            neutral);

        try
        {
            Assert.AreEqual("Bonjour", manager.GetString("Greeting", CultureInfo.GetCultureInfo("fr")));
        }
        finally
        {
            manager.ReleaseAllResources();
            neutral.ReleaseAllResources();
        }
    }

    [TestMethod]
    public void ReleaseAllResources_CompletedLookup_ReloadsStreamSource()
    {
        byte[] first = ResourceTestUtilities.WriteResources(("Greeting", "First"));
        byte[] second = ResourceTestUtilities.WriteResources(("Greeting", "Second"));
        int invocationCount = 0;
        StringResourceManager manager = new(
            "Strings",
            () => new MemoryStream(++invocationCount == 1 ? first : second, writable: false));

        try
        {
            Assert.AreEqual("First", manager.GetString("Greeting"));
            manager.ReleaseAllResources();
            Assert.AreEqual("Second", manager.GetString("Greeting"));
            Assert.AreEqual(2, invocationCount);
        }
        finally
        {
            manager.ReleaseAllResources();
        }
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
