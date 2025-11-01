// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Tool.Dsc;

internal static class DscWriter
{
    /// <summary>
    /// Writes an error message to stderr in DSC JSON format.
    /// </summary>
    public static void WriteError(string message)
    {
        var errorMessage = new DscErrorMessage { Error = message };
        string json = JsonSerializer.Serialize(errorMessage);
        Reporter.Error.WriteLine(json);
    }

    /// <summary>
    /// Writes a debug message to stderr in DSC JSON format.
    /// </summary>
    public static void WriteDebug(string message)
    {
        var debugMessage = new DscDebugMessage { Debug = message };
        string json = JsonSerializer.Serialize(debugMessage);
        Reporter.Error.WriteLine(json);
    }

    /// <summary>
    /// Writes a trace message to stderr in DSC JSON format.
    /// </summary>
    public static void WriteTrace(string message)
    {
        var traceMessage = new DscTraceMessage { Trace = message };
        string json = JsonSerializer.Serialize(traceMessage);
        Reporter.Error.WriteLine(json);
    }

    /// <summary>
    /// Writes the result state to stdout in DSC JSON format.
    /// </summary>
    public static void WriteResult(DscToolsState state)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        string json = JsonSerializer.Serialize(state, options);
        Reporter.Output.WriteLine(json);
    }

    /// <summary>
    /// Writes any object to stdout in JSON format.
    /// </summary>
    public static void WriteJson(object obj, bool writeIndented = false)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented
        };

        string json = JsonSerializer.Serialize(obj, options);
        Reporter.Output.WriteLine(json);
    }

    /// <summary>
    /// Reads input from either stdin (if input is "-") or from a file.
    /// </summary>
    public static string ReadInput(string input)
    {
        if (input == "-")
        {
            return Console.In.ReadToEnd();
        }
        else
        {
            return File.ReadAllText(input);
        }
    }
}
