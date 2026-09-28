namespace Microsoft.AspNetCore.Watch.BrowserRefresh;

public class ScriptInjectingStreamTests
{
    private static readonly string s_injectedScript = ScriptInjectingStream.InjectedScript;

    [Fact]
    public void Write_CompleteBodyTagInSingleWrite_InjectsScript()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);
        var html = "<html><body>Content</body></html>";

        // Act
        stream.Write(Encoding.UTF8.GetBytes(html));

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal($"<html><body>Content{s_injectedScript}</body></html>", result);
        Assert.True(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public async Task WriteAsync_CompleteBodyTagInSingleWrite_InjectsScript()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);
        var html = "<html><body>Content</body></html>";

        // Act
        await stream.WriteAsync(Encoding.UTF8.GetBytes(html));

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal($"<html><body>Content{s_injectedScript}</body></html>", result);
        Assert.True(stream.ScriptInjectionPerformed);
    }

    [Theory]
    [InlineData("<html><body>Content^<", "/body></html>")]
    [InlineData("<html><body>Content^</", "body></html>")]
    [InlineData("<html><body>Content^</b", "ody></html>")]
    [InlineData("<html><body>Content^</bo", "dy></html>")]
    [InlineData("<html><body>Content^</bod", "y></html>")]
    [InlineData("<html><body>Content^</body", "></html>")]
    [InlineData("<html><body>Content^", "<", "/body></html>")]
    [InlineData("<html><body>C", "o", "ntent^", "<", "/body></html>")]
    [InlineData("<html><body>Content^", "</", "body", "></html>")]
    [InlineData("<html><body>Content", "</", "^</", "body></html>")]
    public void Write_BodyTagSplitAcrossMultipleWrites_InjectsScript(params string[] parts)
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);
        var expectedResult = string.Concat(parts).Replace("^", s_injectedScript);

        // Act
        foreach (var part in parts)
        {
            stream.Write(Encoding.UTF8.GetBytes(part.Replace("^", "")));
        }

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal(expectedResult, result);
        Assert.True(stream.ScriptInjectionPerformed);
    }

    [Theory]
    [InlineData("<html><body>Content^<", "/body></html>")]
    [InlineData("<html><body>Content^</", "body></html>")]
    [InlineData("<html><body>Content^</b", "ody></html>")]
    [InlineData("<html><body>Content^</bo", "dy></html>")]
    [InlineData("<html><body>Content^</bod", "y></html>")]
    [InlineData("<html><body>Content^</body", "></html>")]
    [InlineData("<html><body>Content^", "<", "/body></html>")]
    [InlineData("<html><body>C", "o", "ntent^", "<", "/body></html>")]
    [InlineData("<html><body>Content^", "</", "body", "></html>")]
    [InlineData("<html><body>Content", "</", "^</", "body></html>")]
    public async Task WriteAsync_BodyTagSplitAcrossMultipleWrites_InjectsScript(params string[] parts)
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);
        var expectedResult = string.Concat(parts).Replace("^", s_injectedScript);

        // Act
        foreach (var part in parts)
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(part.Replace("^", "")));
        }

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal(expectedResult, result);
        Assert.True(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public void Dispose_FlushesPartialBodyTagAtEndOfInput()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);

        // Act
        stream.Write(Encoding.UTF8.GetBytes("<html><head>Content</head></html></bod"));
        var writeResult = Encoding.UTF8.GetString(baseStream.ToArray());

        stream.Dispose();
        var flushResult = Encoding.UTF8.GetString(baseStream.ToArray());

        // Assert
        Assert.Equal("<html><head>Content</head></html>", writeResult);
        Assert.Equal("<html><head>Content</head></html></bod", flushResult);
        Assert.False(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public async Task DisposeAsync_FlushesPartialBodyTagAtEndOfInput()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);

        // Act
        await stream.WriteAsync(Encoding.UTF8.GetBytes("<html><head>Content</head></html></bod"));
        var writeResult = Encoding.UTF8.GetString(baseStream.ToArray());

        await stream.DisposeAsync();
        var flushResult = Encoding.UTF8.GetString(baseStream.ToArray());

        // Assert
        Assert.Equal("<html><head>Content</head></html>", writeResult);
        Assert.Equal("<html><head>Content</head></html></bod", flushResult);
        Assert.False(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public void Write_BodyTagSplitAcrossMultipleSingleByteWrites_InjectsScript()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);

        // Act - Split "</body>" across 7 writes
        stream.Write(Encoding.UTF8.GetBytes("<html><body>Content<"));
        stream.Write(Encoding.UTF8.GetBytes("/"));
        stream.Write(Encoding.UTF8.GetBytes("b"));
        stream.Write(Encoding.UTF8.GetBytes("o"));
        stream.Write(Encoding.UTF8.GetBytes("d"));
        stream.Write(Encoding.UTF8.GetBytes("y"));
        stream.Write(Encoding.UTF8.GetBytes("></html>"));

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal($"<html><body>Content{s_injectedScript}</body></html>", result);
        Assert.True(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public async Task WriteAsync_BodyTagSplitAcrossMultipleSingleByteWrites_InjectsScript()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);

        // Act - Split "</body>" across 7 writes
        await stream.WriteAsync(Encoding.UTF8.GetBytes("<html><body>Content<"));
        await stream.WriteAsync(Encoding.UTF8.GetBytes("/"));
        await stream.WriteAsync(Encoding.UTF8.GetBytes("b"));
        await stream.WriteAsync(Encoding.UTF8.GetBytes("o"));
        await stream.WriteAsync(Encoding.UTF8.GetBytes("d"));
        await stream.WriteAsync(Encoding.UTF8.GetBytes("y"));
        await stream.WriteAsync(Encoding.UTF8.GetBytes("></html>"));

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal($"<html><body>Content{s_injectedScript}</body></html>", result);
        Assert.True(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public void Write_FalsePositivePartialMatch_FlushesCorrectly()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);

        // Act - Start with partial match that turns out false
        stream.Write(Encoding.UTF8.GetBytes("<html><body>Content</b"));
        stream.Write(Encoding.UTF8.GetBytes("r>Not a body tag</body></html>"));

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal($"<html><body>Content</br>Not a body tag{s_injectedScript}</body></html>", result);
        Assert.True(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public async Task WriteAsync_FalsePositivePartialMatch_FlushesCorrectly()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);

        // Act - Start with partial match that turns out false
        await stream.WriteAsync(Encoding.UTF8.GetBytes("<html><body>Content</b"));
        await stream.WriteAsync(Encoding.UTF8.GetBytes("r>Not a body tag</body></html>"));

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal($"<html><body>Content</br>Not a body tag{s_injectedScript}</body></html>", result);
        Assert.True(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public void Write_NoBodyTag_NoInjection()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);
        var html = "<html><div>Content</div></html>";

        // Act
        stream.Write(Encoding.UTF8.GetBytes(html));

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal(html, result);
        Assert.False(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public async Task WriteAsync_NoBodyTag_NoInjection()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);
        var html = "<html><div>Content</div></html>";

        // Act
        await stream.WriteAsync(Encoding.UTF8.GetBytes(html));

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal(html, result);
        Assert.False(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public void Write_MultipleBodyTags_InjectsOnlyOnce()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);

        // Act
        stream.Write(Encoding.UTF8.GetBytes("<html><body>First</body>"));
        stream.Write(Encoding.UTF8.GetBytes("<body>Second</body></html>"));

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal($"<html><body>First{s_injectedScript}</body><body>Second</body></html>", result);
        Assert.True(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public async Task WriteAsync_MultipleBodyTags_InjectsOnlyOnce()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);

        // Act
        await stream.WriteAsync(Encoding.UTF8.GetBytes("<html><body>First</body>"));
        await stream.WriteAsync(Encoding.UTF8.GetBytes("<body>Second</body></html>"));

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal($"<html><body>First{s_injectedScript}</body><body>Second</body></html>", result);
        Assert.True(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public void WriteByte_PassesThroughDirectly()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);

        // Act
        stream.WriteByte(65); // 'A'
        stream.WriteByte(66); // 'B'

        // Assert
        var result = baseStream.ToArray();
        Assert.Equal(new byte[] { 65, 66 }, result);
        Assert.False(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public void Write_EmptyBuffer_DoesNothing()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);

        // Act
        stream.Write(ReadOnlySpan<byte>.Empty);

        // Assert
        Assert.Empty(baseStream.ToArray());
        Assert.False(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public async Task WriteAsync_EmptyBuffer_DoesNothing()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);

        // Act
        await stream.WriteAsync(ReadOnlyMemory<byte>.Empty);

        // Assert
        Assert.Empty(baseStream.ToArray());
        Assert.False(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public async Task WriteAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            // Use a mock stream that respects cancellation
            var mockStream = new CancellationTestStream();
            var testStream = new ScriptInjectingStream(mockStream);
            await testStream.WriteAsync(Encoding.UTF8.GetBytes("test"), cts.Token);
        });
    }

    [Fact]
    public void Write_ArrayOverload_CompleteBodyTag_InjectsScript()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);
        var html = "<html><body>Content</body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);

        // Act
        stream.Write(bytes, 0, bytes.Length);

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal($"<html><body>Content{s_injectedScript}</body></html>", result);
        Assert.True(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public async Task WriteAsync_ArrayOverload_CompleteBodyTag_InjectsScript()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);
        var html = "<html><body>Content</body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);

        // Act
        await stream.WriteAsync(bytes, 0, bytes.Length);

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal($"<html><body>Content{s_injectedScript}</body></html>", result);
        Assert.True(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public void Write_ArrayOverloadWithOffset_InjectsScript()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);
        var buffer = Encoding.UTF8.GetBytes("XXX<html><body>Content</body></html>YYY");

        // Act
        stream.Write(buffer, 3, buffer.Length - 6); // Skip XXX and YYY

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal($"<html><body>Content{s_injectedScript}</body></html>", result);
        Assert.True(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public async Task WriteAsync_ArrayOverloadWithOffset_InjectsScript()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);
        var buffer = Encoding.UTF8.GetBytes("XXX<html><body>Content</body></html>YYY");

        // Act
        await stream.WriteAsync(buffer, 3, buffer.Length - 6); // Skip XXX and YYY

        // Assert
        var result = Encoding.UTF8.GetString(baseStream.ToArray());
        Assert.Equal($"<html><body>Content{s_injectedScript}</body></html>", result);
        Assert.True(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public void Flush_WithoutAnyWrites_DoesNotCrash()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);

        // Act
        stream.Flush();

        // Assert
        Assert.Empty(baseStream.ToArray());
        Assert.False(stream.ScriptInjectionPerformed);
    }

    [Fact]
    public async Task FlushAsync_WithoutAnyWrites_DoesNotCrash()
    {
        // Arrange
        var baseStream = new MemoryStream();
        var stream = new ScriptInjectingStream(baseStream);

        // Act
        await stream.FlushAsync();

        // Assert
        Assert.Empty(baseStream.ToArray());
        Assert.False(stream.ScriptInjectionPerformed);
    }

    private class CancellationTestStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get; set; }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) { }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, System.Threading.CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }
}
