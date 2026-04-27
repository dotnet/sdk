// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils;

using System.Diagnostics.CodeAnalysis;


public interface IEnvironmentProvider
{
    IEnumerable<string> ExecutableExtensions { get; }

    string? GetCommandPath(string commandName, params string[] extensions);

    string? GetCommandPathFromRootPath(string rootPath, string commandName, params string[] extensions);

    string? GetCommandPathFromRootPath(string rootPath, string commandName, IEnumerable<string> extensions);

    bool GetEnvironmentVariableAsBool(string name, bool defaultValue);

    int? GetEnvironmentVariableAsNullableInt(string name);

    string? GetEnvironmentVariable(string name);

    bool TryGetEnvironmentVariable(string name, [NotNullWhen(true)] out string? value);
    bool TryGetEnvironmentVariableAsBool(string name, [NotNullWhen(true)] out bool value);
    bool TryGetEnvironmentVariableAsInt(string name, [NotNullWhen(true)] out int value);

    string? GetEnvironmentVariable(string variable, EnvironmentVariableTarget target);

    void SetEnvironmentVariable(string variable, string value, EnvironmentVariableTarget target);
}
