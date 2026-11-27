// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class ContentStoreContentionTests
{
    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows)]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public async Task Commit_waits_for_a_temporarily_exclusive_destination(bool cancel, bool corrupt)
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string temporary = Path.Combine(root, "download.tmp");
        string destination = Path.Combine(root, "blob");
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        FileStream? held = null;
        Task? commit = null;
        try
        {
            File.WriteAllText(temporary, "download");
            File.WriteAllText(destination, corrupt ? "corrupt" : "download");
            held = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.None);
            TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            commit = Task.Run(() =>
            {
                started.SetResult();
                ContentStore.CommitBlob(temporary, destination, DigestUtils.ComputeSha256Digest("download"), cancellation.Token);
            }, TestContext.CancellationToken);
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);
            await Task.Delay(100, TestContext.CancellationToken);
            Assert.IsFalse(commit.IsCompleted, "Commit must wait until the competing file operation releases its exclusive handle.");
            if (cancel)
            {
                cancellation.Cancel();
                await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => commit.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken));
            }
            else
            {
                held.Dispose();
                held = null;
                if (corrupt)
                {
                    await Assert.ThrowsExactlyAsync<InvalidDigestException>(() => commit.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken));
                }
                else
                {
                    await commit.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);
                    Assert.AreEqual("download", File.ReadAllText(destination));
                }
            }
            Assert.IsFalse(File.Exists(temporary));
        }
        finally
        {
            cancellation.Cancel();
            held?.Dispose();
            if (commit is not null)
            {
                try { await commit; }
                catch (Exception) when (commit.IsFaulted || commit.IsCanceled) { }
            }
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(ConditionMode.Include, OperatingSystems.Windows)]
    public async Task Commit_does_not_wait_indefinitely_for_an_exclusive_destination()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string temporary = Path.Combine(root, "download.tmp");
        string destination = Path.Combine(root, "blob");
        try
        {
            File.WriteAllText(temporary, "download");
            File.WriteAllText(destination, "download");
            using FileStream held = new(destination, FileMode.Open, FileAccess.Read, FileShare.None);
            Task commit = Task.Run(() => ContentStore.CommitBlob(temporary, destination,
                DigestUtils.ComputeSha256Digest("download"), TestContext.CancellationToken), TestContext.CancellationToken);
            await Assert.ThrowsExactlyAsync<IOException>(() => commit.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken));
            Assert.IsFalse(File.Exists(temporary));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
