// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks.UnitTests;

// Other tests can resolve relative paths without acquiring a CurrentDirectory resource lock.
[DoNotParallelize]
[TestClass]
public sealed class GivenAnEnsureHotReloadWebSocketKey : SdkTest
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void KeyFilesAreIsolatedByProjectAndReused(bool absolutePaths, bool bareFileNames)
    {
        string[] projectDirectories =
        [
            TestAssetsManager.CreateTestDirectory(identifier: $"{absolutePaths}_{bareFileNames}_first").Path,
            TestAssetsManager.CreateTestDirectory(identifier: $"{absolutePaths}_{bareFileNames}_second").Path,
        ];
        string decoyDirectory = TestAssetsManager.CreateTestDirectory(identifier: $"{absolutePaths}_{bareFileNames}_decoy").Path;
        string originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(decoyDirectory);
            var tasks = projectDirectories.Select(directory => new EnsureHotReloadWebSocketKey
            {
                BuildEngine = new MockBuildEngine(),
                TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(directory),
                PublicKeyPath = absolutePaths
                    ? Path.Combine(directory, "keys", "public.json")
                    : bareFileNames ? "public.json" : Path.Combine("obj", "..", "keys", "public.json"),
                PrivateKeyPath = absolutePaths
                    ? Path.Combine(directory, "keys", "private.json")
                    : bareFileNames ? "private.json" : Path.Combine("obj", "..", "keys", "private.json"),
            }).ToArray();

            Parallel.ForEach(tasks, task => Assert.IsTrue(task.Execute()));
            Assert.AreNotEqual(tasks[0].PublicKey, tasks[1].PublicKey);

            for (int i = 0; i < tasks.Length; i++)
            {
                string keyDirectory = bareFileNames ? projectDirectories[i] : Path.Combine(projectDirectories[i], "keys");
                string publicFile = Path.Combine(keyDirectory, "public.json");
                string privateFile = Path.Combine(keyDirectory, "private.json");
                using var publicDocument = JsonDocument.Parse(File.ReadAllBytes(publicFile));
                using var privateDocument = JsonDocument.Parse(File.ReadAllBytes(privateFile));
                Assert.AreEqual(tasks[i].PublicKey, publicDocument.RootElement.GetProperty("publicKey").GetString());
                Assert.AreEqual(tasks[i].PublicKey, privateDocument.RootElement.GetProperty("publicKey").GetString());
                byte[] privateContent = File.ReadAllBytes(privateFile);
                var timestamp = new DateTime(2001, 2, 3, 0, 0, 0, DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(publicFile, timestamp);
                File.SetLastWriteTimeUtc(privateFile, timestamp);

                Assert.IsTrue(tasks[i].Execute());

                Assert.AreSequenceEqual(privateContent, File.ReadAllBytes(privateFile));
                Assert.AreEqual(timestamp, File.GetLastWriteTimeUtc(publicFile));
                Assert.AreEqual(timestamp, File.GetLastWriteTimeUtc(privateFile));
            }

            Assert.IsEmpty(Directory.GetFileSystemEntries(decoyDirectory));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [TestMethod]
    [DataRow(null, true)]
    [DataRow("", true)]
    [DataRow(null, false)]
    [DataRow("", false)]
    public void InvalidKeyPathsPreserveTheirException(string? invalidPath, bool publicPath)
    {
        AssertInvalidKeyPathBehavior(invalidPath, publicPath);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(true)]
    [DataRow(false)]
    public void WhitespaceKeyPathsPreserveTheirExceptionOnWindows(bool publicPath)
    {
        AssertInvalidKeyPathBehavior(" ", publicPath);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(true)]
    [DataRow(false)]
    public void WhitespaceParentPreservesCreationFailureBeforeWritingKeys(bool publicPath)
    {
        string directory = TestAssetsManager.CreateTestDirectory(identifier: publicPath.ToString()).Path;
        string publicFile = Path.Combine(directory, "public.json");
        string privateFile = Path.Combine(directory, "private.json");
        string invalidPath = Path.Combine(" ", $"key-{Guid.NewGuid():N}.json");
        var task = new EnsureHotReloadWebSocketKey
        {
            BuildEngine = new MockBuildEngine(),
            TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(directory),
            PublicKeyPath = publicPath ? invalidPath : publicFile,
            PrivateKeyPath = publicPath ? privateFile : invalidPath,
        };
        var expected = Assert.Throws<ArgumentException>(() => Directory.CreateDirectory(" "));

        var actual = Assert.Throws<ArgumentException>(() => task.Execute());

        Assert.AreEqual(expected.GetType(), actual.GetType());
        Assert.AreEqual(expected.Message, actual.Message);
        Assert.IsFalse(File.Exists(publicFile));
        Assert.IsFalse(File.Exists(privateFile));
    }

    private void AssertInvalidKeyPathBehavior(string? invalidPath, bool publicPath)
    {
        string directory = TestAssetsManager.CreateTestDirectory(
            identifier: $"{invalidPath ?? "null"}_{publicPath}").Path;
        string privateKeyPath = Path.Combine(directory, "private.json");
        var task = new EnsureHotReloadWebSocketKey
        {
            BuildEngine = new MockBuildEngine(),
            TaskEnvironment = TaskEnvironment.CreateWithProjectDirectoryAndEnvironment(directory),
            PublicKeyPath = publicPath ? invalidPath! : Path.Combine(directory, "public.json"),
            PrivateKeyPath = publicPath ? privateKeyPath : invalidPath!,
        };

        var exception = Assert.Throws<ArgumentException>(() => task.Execute());

        Assert.AreEqual(invalidPath is null ? typeof(ArgumentNullException) : typeof(ArgumentException), exception.GetType());
        Assert.AreEqual("path", exception.ParamName);
        Assert.AreEqual(publicPath, File.Exists(privateKeyPath));
    }
}
