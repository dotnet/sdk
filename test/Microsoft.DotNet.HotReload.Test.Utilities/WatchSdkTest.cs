// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.DotNet.Watch.UnitTests;

public abstract class WatchSdkTest(ITestOutputHelper logger)
    : SdkTest(new DebugTestOutputLogger(logger))
{
    public TestAssetsManager TestAssets
        => TestAssetsManager;

    public DebugTestOutputLogger Logger
        => (DebugTestOutputLogger)base.Log;

    public new void Log(string message, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        => Logger.Log(message, testPath, testLine);

    public void UpdateSourceFile(string path, string text, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
    {
        var existed = File.Exists(path);
        WriteAllText(path, text);
        Log($"File '{path}' " + (existed ? "updated" : "added"), testPath, testLine);
    }

    public void UpdateSourceFile(string path, Func<string, string> contentTransform, [CallerFilePath] string? testPath = null, [CallerLineNumber] int testLine = 0)
        => UpdateSourceFile(path, contentTransform(File.ReadAllText(path, Encoding.UTF8)), testPath, testLine);

    /// <summary>
    /// Replacement for <see cref="File.WriteAllText"/>, which fails to write to hidden file
    /// </summary>
    public static void WriteAllText(string path, string text)
    {
        using var stream = File.Open(path, FileMode.OpenOrCreate);

        using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(text);
        }

        // truncate the rest of the file content:
        stream.SetLength(stream.Position);
    }

    public void UpdateSourceFile(string path)
        => UpdateSourceFile(path, content => content);
}
