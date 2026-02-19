// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

/// <summary>
/// Extracts PII-safe diagnostic metadata from exceptions for telemetry:
/// source location (stack-trace inspection) and exception-type chains.
/// </summary>
/// <remarks>
/// All output is designed to be safe for telemetry ingestion:
/// <list type="bullet">
///   <item>Source locations contain only type/method names and line numbers from our
///         assemblies — never file paths, user names, or arguments.</item>
///   <item>Exception chains contain only CLR type names, which are stable and non-PII.</item>
/// </list>
/// Uses <see cref="DiagnosticMethodInfo"/> (.NET 8+) for AOT-compatible stack frame inspection.
/// </remarks>
internal static class ExceptionInspector
{
    /// <summary>
    /// Gets a safe source location from the stack trace — finds the first frame from our assemblies.
    /// This is typically the code in dotnetup that called into BCL/external code that threw.
    /// No file paths that could contain user info. Line numbers from our code are included as they are not PII.
    /// </summary>
    internal static string? GetSafeSourceLocation(Exception ex)
    {
        try
        {
            var stackTrace = new StackTrace(ex, fNeedFileInfo: true);
            var frames = stackTrace.GetFrames();

            if (frames == null || frames.Length == 0)
            {
                return null;
            }

            string? throwSite = null;

            // Walk the stack from throw site upward, looking for the first frame in our code.
            // This finds the dotnetup code that called into BCL/external code that threw.
            foreach (var frame in frames)
            {
                var methodInfo = DiagnosticMethodInfo.Create(frame);
                if (methodInfo == null) continue;

                // DiagnosticMethodInfo provides DeclaringTypeName which includes the full type name
                var declaringType = methodInfo.DeclaringTypeName;
                if (string.IsNullOrEmpty(declaringType)) continue;

                // Capture the first frame as the throw site (fallback)
                if (throwSite == null)
                {
                    var throwTypeName = ExtractTypeName(declaringType);
                    throwSite = $"[BCL]{throwTypeName}.{methodInfo.Name}";
                }

                // Check if it's from our assemblies by looking at the namespace prefix
                if (IsOwnedNamespace(declaringType))
                {
                    // Extract just the type name (last part after the last dot, before any generic params)
                    var typeName = ExtractTypeName(declaringType);

                    // Include line number for our code (not PII), but never file paths
                    var lineNumber = frame.GetFileLineNumber();
                    var location = $"{typeName}.{methodInfo.Name}";
                    if (lineNumber > 0)
                    {
                        location += $":{lineNumber}";
                    }
                    return location;
                }
            }

            // If we didn't find our code, return the throw site as a fallback.
            // The throw site is from BCL or our NuGet dependencies (e.g., System.IO, System.Net)
            return throwSite;
        }
        catch
        {
            // Never fail telemetry due to stack trace parsing
            return null;
        }
    }

    /// <summary>
    /// Gets the exception type chain for wrapped exceptions.
    /// Example: "HttpRequestException->SocketException"
    /// </summary>
    internal static string? GetExceptionChain(Exception ex)
    {
        if (ex.InnerException == null)
        {
            return null;
        }

        try
        {
            var types = new List<string> { ex.GetType().Name };
            var inner = ex.InnerException;

            // Limit depth to prevent infinite loops and overly long strings
            const int maxDepth = 5;
            var depth = 0;

            while (inner != null && depth < maxDepth)
            {
                types.Add(inner.GetType().Name);
                inner = inner.InnerException;
                depth++;
            }

            return string.Join("->", types);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if a type name belongs to one of our owned namespaces.
    /// </summary>
    private static bool IsOwnedNamespace(string declaringType)
    {
        return declaringType.StartsWith("Microsoft.DotNet.Tools.Bootstrapper", StringComparison.Ordinal) ||
               declaringType.StartsWith("Microsoft.Dotnet.Installation", StringComparison.Ordinal);
    }

    /// <summary>
    /// Extracts just the type name from a fully qualified type name.
    /// </summary>
    private static string ExtractTypeName(string fullTypeName)
    {
        var typeName = fullTypeName;
        var lastDot = typeName.LastIndexOf('.');
        if (lastDot >= 0)
        {
            typeName = typeName.Substring(lastDot + 1);
        }
        // Remove generic arity if present (e.g., "List`1" -> "List")
        var genericMarker = typeName.IndexOf('`');
        if (genericMarker >= 0)
        {
            typeName = typeName.Substring(0, genericMarker);
        }
        return typeName;
    }
}
