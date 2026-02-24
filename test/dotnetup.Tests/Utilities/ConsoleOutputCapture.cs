// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

/// <summary>
/// Helper class to capture console output for testing
/// </summary>
internal class ConsoleOutputCapture : IDisposable
{
    private readonly TextWriter _originalConsoleOut;
    private readonly StringBuilder _stringBuilder;
    private readonly StringWriter _stringWriter;

    public ConsoleOutputCapture()
    {
        _originalConsoleOut = Console.Out;
        _stringBuilder = new StringBuilder();
        _stringWriter = new StringWriter(_stringBuilder);
        Console.SetOut(_stringWriter);
    }

    public string GetOutput()
    {
        _stringWriter.Flush();
        return _stringBuilder.ToString();
    }

    public void Dispose()
    {
        Console.SetOut(_originalConsoleOut);
        _stringWriter.Dispose();
    }
}
