// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http.Headers;
using Microsoft.DotNet.Cli.Commands.Test;
using Microsoft.DotNet.Cli.Commands.Test.IPC;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Models;
using Microsoft.DotNet.Cli.Commands.Test.IPC.Serializers;
using Microsoft.DotNet.Cli.Utils;

namespace dotnet.Tests.CommandTests.Test;

[TestClass]
public class FileArtifactTransferTests : SdkTest
{
    [TestMethod]
    public async Task HttpReceiverRequiresAHandshakeAndReportsOnlyCommittedPaths()
    {
        string directory = TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path;
        var artifacts = new ArtifactPostProcessingManager();
        using TestApplication application = TestApplicationLaunchTests.CreateApplication(
            new TestOptions(false, false, TestListFormat.Text),
            runtimeIdentifier: "browser-wasm",
            resultsDirectory: directory,
            artifactManager: artifacts);
        application.CreateProcessStartInfo();
        var chunk = Chunk("transfer", "file.txt", 3, 0, [1, 2, 3], true);

        FileArtifactTransferResponse rejected = Assert.IsInstanceOfType<FileArtifactTransferResponse>(
            await SendHttpAsync(application, chunk));
        Assert.IsNotNull(rejected.Error);
        Assert.IsEmpty(Directory.GetFiles(directory, "*", SearchOption.AllDirectories));

        var handshake = new HandshakeMessage(new Dictionary<byte, string>
        {
            [HandshakeMessagePropertyNames.ExecutionId] = "execution",
            [HandshakeMessagePropertyNames.InstanceId] = "instance",
            [HandshakeMessagePropertyNames.HostType] = HandshakeMessageHostTypes.TestHost,
            [HandshakeMessagePropertyNames.Framework] = ".NET 11.0",
            [HandshakeMessagePropertyNames.Architecture] = "wasm",
            [HandshakeMessagePropertyNames.SupportedProtocolVersions] = "1.3.0;1.5.0",
        });
        HandshakeMessage reply = Assert.IsInstanceOfType<HandshakeMessage>(await SendHttpAsync(application, handshake));
        Assert.AreEqual("1.5.0", reply.Properties[HandshakeMessagePropertyNames.SupportedProtocolVersions]);
        Assert.AreEqual("chunked-v1", reply.Properties[HandshakeMessagePropertyNames.FileArtifactTransfer]);

        rejected = Assert.IsInstanceOfType<FileArtifactTransferResponse>(
            await SendHttpAsync(application, chunk with { InstanceId = "not-the-handshaken-instance" }));
        Assert.IsNotNull(rejected.Error);

        FileArtifactTransferResponse committed = Assert.IsInstanceOfType<FileArtifactTransferResponse>(
            await SendHttpAsync(application, chunk));
        Assert.IsNull(committed.Error);
        Assert.IsNotNull(committed.FullPath);
        Assert.AreSequenceEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(committed.FullPath));
        Assert.IsFalse(Path.GetRelativePath(directory, committed.FullPath).StartsWith("..", StringComparison.Ordinal));

        await SendHttpAsync(application, new FileArtifactMessages("execution", "instance",
            [new FileArtifactMessage(committed.FullPath, "exported", null, null, null, null, null)]));
        Assert.IsTrue(artifacts.ContainsArtifactPath(committed.FullPath));

        string forgedPath = Path.Combine(directory, "not-exported.txt");
        File.WriteAllText(forgedPath, "unchanged");
        await SendHttpAsync(application, new FileArtifactMessages("execution", "instance",
            [new FileArtifactMessage(forgedPath, "not-exported", null, null, null, null, null)]));
        Assert.IsFalse(artifacts.ContainsArtifactPath(forgedPath));
        Assert.AreEqual("unchanged", File.ReadAllText(forgedPath));

        Task<IResponse> control = SendHttpAsync(application, WaitForServerControlRequest.CachedInstance);
        application.RequestSessionCancellation();
        ServerControlMessage cancellation = Assert.IsInstanceOfType<ServerControlMessage>(
            await control.WaitAsync(TimeSpan.FromSeconds(30), TestContext.CancellationToken));
        Assert.AreEqual(ServerControlKinds.CancelSession, cancellation.Kind);
    }

    [TestMethod]
    public void WireContractRoundTripsChunksAndErrors()
    {
        var serializer = new ProtocolMessageSerializer();
        serializer.RegisterAllSerializers();
        var request = Chunk("transfer", "report.trx", 4, 0, [0, 1, 2, 255], true);
        byte[] frame = serializer.Serialize(request);
        Assert.AreEqual(15, BitConverter.ToInt32(frame.AsSpan(sizeof(int))));
        var decoded = Assert.IsInstanceOfType<FileArtifactTransferRequest>(serializer.Deserialize(frame, false));
        Assert.AreEqual(request.ExecutionId, decoded.ExecutionId);
        Assert.AreEqual(request.InstanceId, decoded.InstanceId);
        Assert.AreEqual(request.TransferId, decoded.TransferId);
        Assert.AreEqual(request.FileName, decoded.FileName);
        Assert.AreEqual(request.TotalLength, decoded.TotalLength);
        Assert.AreEqual(request.Offset, decoded.Offset);
        Assert.AreEqual(request.IsFinal, decoded.IsFinal);
        Assert.AreSequenceEqual(request.Data, decoded.Data);

        var response = new FileArtifactTransferResponse(0, null, "quota exceeded");
        frame = serializer.Serialize(response);
        Assert.AreEqual(16, BitConverter.ToInt32(frame.AsSpan(sizeof(int))));
        Assert.AreEqual(response, serializer.Deserialize(frame, false));
    }

    [TestMethod]
    public void DecoderRejectsMissingRequiredFields()
    {
        var serializer = new ProtocolMessageSerializer();
        serializer.RegisterAllSerializers();
        byte[] frame = serializer.Serialize(Chunk("transfer", "file.txt", 0, 0, [], true) with { ExecutionId = null });
        Assert.ThrowsExactly<InvalidDataException>(() => serializer.Deserialize(frame, false));
    }

    [TestMethod]
    [DataRow(-1L, 0L)]
    [DataRow(0L, -1L)]
    public void DecoderRejectsNegativeLengths(long length, long offset)
    {
        var serializer = new ProtocolMessageSerializer();
        serializer.RegisterAllSerializers();
        byte[] frame = serializer.Serialize(Chunk("transfer", "file.txt", length, offset, [], true));
        Assert.ThrowsExactly<InvalidDataException>(() => serializer.Deserialize(frame, false));
    }

    [TestMethod]
    public void DecoderRejectsOversizedChunks()
    {
        var serializer = new ProtocolMessageSerializer();
        serializer.RegisterAllSerializers();
        byte[] data = new byte[FileArtifactTransferRequest.MaximumChunkBytes + 1];
        byte[] frame = serializer.Serialize(Chunk("transfer", "file.txt", data.Length, 0, data, true));
        Assert.ThrowsExactly<InvalidDataException>(() => serializer.Deserialize(frame, false));
    }

    [TestMethod]
    public void CommitsOnlyAfterTheFinalContiguousChunk()
    {
        using FileArtifactTransferManager manager = CreateManager(out string directory, out List<string> errors);
        FileArtifactTransferResponse first = manager.Receive(Chunk("transfer", "report.trx", 4, 0, [1, 2], false));

        Assert.IsNull(first.Error);
        Assert.IsNull(first.FullPath);
        Assert.AreEqual(2L, first.NextOffset);
        Assert.IsTrue(manager.HasIncompleteOrFailedTransfers);
        Assert.AreEqual(".partial", Path.GetFileName(Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Single()));

        FileArtifactTransferResponse final = manager.Receive(Chunk("transfer", "report.trx", 4, 2, [3, 4], true));

        Assert.IsNull(final.Error);
        Assert.IsNotNull(final.FullPath);
        Assert.AreEqual(4L, final.NextOffset);
        Assert.AreSequenceEqual(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(final.FullPath));
        Assert.IsTrue(manager.OwnsPublishedPath(final.FullPath));
        Assert.IsFalse(manager.HasIncompleteOrFailedTransfers);
        Assert.IsEmpty(errors);
    }

    [TestMethod]
    public void EmptyArtifactsAreCommittedAndCountTowardTheFileQuota()
    {
        using FileArtifactTransferManager manager = CreateManager(
            out _, out _, new FileArtifactTransferLimits(10, 10, 1));
        FileArtifactTransferResponse first = manager.Receive(Chunk("first", "empty.txt", 0, 0, [], true));
        Assert.IsNull(first.Error);
        Assert.IsNotNull(first.FullPath);
        Assert.AreEqual(0L, new FileInfo(first.FullPath).Length);

        FileArtifactTransferResponse second = manager.Receive(Chunk("second", "also-empty.txt", 0, 0, [], true));
        Assert.IsNotNull(second.Error);
        Assert.IsNull(second.FullPath);
        Assert.IsTrue(manager.HasIncompleteOrFailedTransfers);
    }

    [TestMethod]
    public void ModulesWithIdenticalFileNamesRemainIsolated()
    {
        using FileArtifactTransferManager first = CreateManager(out string directory, out List<string> errors);
        using var second = new FileArtifactTransferManager(directory, FileArtifactTransferLimits.Default, errors.Add);
        FileArtifactTransferResponse firstFile = first.Receive(Chunk("same-id", "same.txt", 1, 0, [1], true));
        FileArtifactTransferResponse secondFile = second.Receive(Chunk("same-id", "same.txt", 1, 0, [2], true));

        Assert.IsNotNull(firstFile.FullPath);
        Assert.IsNotNull(secondFile.FullPath);
        Assert.AreNotEqual(firstFile.FullPath, secondFile.FullPath);
        Assert.AreSequenceEqual(new byte[] { 1 }, File.ReadAllBytes(firstFile.FullPath));
        Assert.AreSequenceEqual(new byte[] { 2 }, File.ReadAllBytes(secondFile.FullPath));
        Assert.IsFalse(first.OwnsPublishedPath(secondFile.FullPath));
        Assert.IsEmpty(errors);
    }

    [TestMethod]
    [DataRow("../escape.txt")]
    [DataRow("..\\escape.txt")]
    [DataRow("/escape.txt")]
    [DataRow("C:\\escape.txt")]
    [DataRow("..")]
    [DataRow(".partial")]
    [DataRow(".PARTIAL")]
    [DataRow("file:stream")]
    [DataRow("file.")]
    public void RejectsUnsafeFileNames(string fileName)
    {
        using FileArtifactTransferManager manager = CreateManager(out string directory, out List<string> errors);
        FileArtifactTransferResponse response = manager.Receive(Chunk("transfer", fileName, 1, 0, [1], true));
        Assert.IsNotNull(response.Error);
        Assert.IsNull(response.FullPath);
        Assert.IsEmpty(Directory.GetFiles(directory, "*", SearchOption.AllDirectories));
        Assert.HasCount(1, errors);
    }

    [TestMethod]
    public void DuplicateCompletionCannotOverwriteTheCommittedFile()
    {
        using FileArtifactTransferManager manager = CreateManager(out string directory, out _);
        FileArtifactTransferResponse first = manager.Receive(Chunk("transfer", "file.txt", 1, 0, [1], true));
        FileArtifactTransferResponse duplicate = manager.Receive(Chunk("transfer", "file.txt", 1, 0, [2], true));
        Assert.IsNotNull(duplicate.Error);
        Assert.IsNotNull(first.FullPath);
        Assert.AreSequenceEqual(new byte[] { 1 }, File.ReadAllBytes(first.FullPath));
        Assert.HasCount(1, Directory.GetFiles(directory, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    [DataRow(1L, true)]
    [DataRow(2L, true)]
    [DataRow(-1L, false)]
    public void InvalidContinuationDeletesThePartialFile(long offset, bool final)
    {
        using FileArtifactTransferManager manager = CreateManager(out string directory, out _);
        Assert.IsNull(manager.Receive(Chunk("transfer", "file.txt", 4, 0, [1, 2], false)).Error);
        FileArtifactTransferResponse response = manager.Receive(Chunk("transfer", "file.txt", 4, offset, [3], final));
        Assert.IsNotNull(response.Error);
        Assert.IsEmpty(Directory.GetFiles(directory, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    public void EnforcesFileAndModuleByteLimits()
    {
        using FileArtifactTransferManager manager = CreateManager(
            out _, out _, new FileArtifactTransferLimits(3, 4, 10));
        Assert.IsNotNull(manager.Receive(Chunk("too-large", "large.txt", 4, 0, [1, 2, 3, 4], true)).Error);
        Assert.IsNull(manager.Receive(Chunk("first", "first.txt", 3, 0, [1, 2, 3], true)).Error);
        Assert.IsNotNull(manager.Receive(Chunk("second", "second.txt", 2, 0, [4, 5], true)).Error);
    }

    [TestMethod]
    public void AbortedTransfersReleaseReservedQuota()
    {
        using FileArtifactTransferManager manager = CreateManager(
            out _, out _, new FileArtifactTransferLimits(4, 4, 1));
        Assert.IsNull(manager.Receive(Chunk("first", "first.txt", 4, 0, [1], false)).Error);
        Assert.IsNotNull(manager.Receive(Chunk("first", "first.txt", 4, 3, [2], true)).Error);
        Assert.IsNull(manager.Receive(Chunk("second", "second.txt", 4, 0, [1, 2, 3, 4], true)).Error);
    }

    [TestMethod]
    public void DisposeRemovesPartialsButKeepsCommittedArtifacts()
    {
        using FileArtifactTransferManager manager = CreateManager(out string directory, out List<string> errors);
        FileArtifactTransferResponse complete = manager.Receive(Chunk("complete", "complete.txt", 1, 0, [1], true));
        Assert.IsNull(manager.Receive(Chunk("partial", "partial.txt", 2, 0, [2], false)).Error);

        manager.Dispose();

        Assert.IsNotNull(complete.FullPath);
        Assert.AreSequenceEqual(new[] { complete.FullPath }, Directory.GetFiles(directory, "*", SearchOption.AllDirectories));
        Assert.IsEmpty(errors);
    }

    [TestMethod]
    public void FileSystemFailuresAreReturnedAndReported()
    {
        using FileArtifactTransferManager manager = CreateManager(out string directory, out List<string> errors);
        File.WriteAllText(Path.Combine(directory, "browser"), "not a directory");
        FileArtifactTransferResponse response = manager.Receive(Chunk("transfer", "file.txt", 1, 0, [1], true));
        Assert.IsNotNull(response.Error);
        Assert.IsNull(response.FullPath);
        Assert.HasCount(1, errors);
    }

    [TestMethod]
    public void LimitsHaveConservativeDefaultsAndExplicitOverrides()
    {
        Assert.AreEqual(FileArtifactTransferLimits.Default, FileArtifactTransferLimits.Parse(null, null, null));
        Assert.AreEqual(new FileArtifactTransferLimits(123, 456, 7), FileArtifactTransferLimits.Parse("123", "456", "7"));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("0")]
    [DataRow("-1")]
    [DataRow("unlimited")]
    [DataRow("9223372036854775808")]
    public void InvalidLimitsAreNotSilentlyIgnored(string value)
    {
        Assert.ThrowsExactly<GracefulException>(() => FileArtifactTransferLimits.Parse(value, null, null));
    }

    private FileArtifactTransferManager CreateManager(
        out string directory,
        out List<string> errors,
        FileArtifactTransferLimits? limits = null)
    {
        directory = TestAssetsManager.CreateTestDirectory(identifier: Guid.NewGuid().ToString("N")).Path;
        errors = [];
        return new FileArtifactTransferManager(directory, limits ?? FileArtifactTransferLimits.Default, errors.Add);
    }

    private static FileArtifactTransferRequest Chunk(
        string id, string name, long length, long offset, byte[] data, bool final)
        => new("execution", "instance", id, name, length, offset, data, final);

    private async Task<IResponse> SendHttpAsync(TestApplication application, object message)
    {
        string[] bootstrap = File.ReadAllLines(application.HttpResponseFilePath!);
        string endpoint = bootstrap.Single(line => line.StartsWith(CliConstants.DotNetTestHttpEndpointOptionKey + " ", StringComparison.Ordinal))
            [(CliConstants.DotNetTestHttpEndpointOptionKey.Length + 1)..];
        string token = bootstrap.Single(line => line.StartsWith(CliConstants.DotNetTestHttpTokenOptionKey + " ", StringComparison.Ordinal))
            [(CliConstants.DotNetTestHttpTokenOptionKey.Length + 1)..];
        var serializer = new ProtocolMessageSerializer();
        serializer.RegisterAllSerializers();
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new ByteArrayContent(serializer.Serialize(message));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.CancellationToken);
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsInstanceOfType<IResponse>(serializer.Deserialize(
            await response.Content.ReadAsByteArrayAsync(TestContext.CancellationToken), false));
    }
}
