// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;

namespace Microsoft.DotNet.ProjectTools;

internal static class MSBuildPropertyEscaping
{
    /// <summary>
    /// Escapes a raw literal value for an MSBuild global property, including command-line syntax.
    /// </summary>
    public static string Escape(string value)
        // MSBuild's command-line property parser also treats commas and double quotes as syntax.
        => ProjectCollection.Escape(value).Replace(",", "%2C").Replace("\"", "%22");
}
