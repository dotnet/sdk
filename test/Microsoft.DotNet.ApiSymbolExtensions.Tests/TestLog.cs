// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiSymbolExtensions.Tests;

internal class TestLog : ILog
{
    public List<string> Info { get; } = [];
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [];

    public bool HasLoggedErrors => Errors.Count != 0;
    public bool HasLoggedWarnings => Warnings.Count != 0;

    public void LogError(string message) => Errors.Add(message);
    public void LogError(string code, string message) => Errors.Add($"{code} {message}");

    public void LogWarning(string message) => Warnings.Add(message);
    public void LogWarning(string code, string message) => Warnings.Add($"{code} {message}");

    public void LogMessage(string message) => Info.Add(message);
    public void LogMessage(MessageImportance importance, string message) => Info.Add(message);
}
