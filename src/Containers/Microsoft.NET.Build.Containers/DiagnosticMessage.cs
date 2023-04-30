// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents a diagnostic message that could be parsed by VS.
/// https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-diagnostic-format-for-tasks?view=vs-2022
/// </summary>
internal static class DiagnosticMessage
{
    public static string Warning(string code, string text) => Create("warning", code, text);

    public static string Error(string code, string text) => Create("error", code, text);

    public static string WarningFromResourceWithCode(string resourceName, params object?[] args) => CreateFromResourceWithCode("warning", resourceName, args);

    public static string ErrorFromResourceWithCode(string resourceName, params object?[] args) => CreateFromResourceWithCode("error", resourceName, args);

    private static string Create(string category, string code, string text)
    {
        StringBuilder builder = new();

        builder.Append("Containerize : "); // tool name as the origin
        builder.Append(category);
        builder.Append(' ');
        builder.Append(code);
        builder.Append(" : ");
        builder.Append(text);

        return builder.ToString();
    }

    private static string CreateFromResourceWithCode(string category, string resourceName, params object?[] args)
    {
        string textWithCode = Resource.FormatString(resourceName, args);

        StringBuilder builder = new();

        builder.Append("Containerize : "); // tool name as the origin
        builder.Append(category);
        builder.Append(' ');
        builder.Append(textWithCode);

        return builder.ToString();
    }
}
