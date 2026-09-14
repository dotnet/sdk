// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Xml;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Run;

/// <summary>
/// Provides utilities for passing environment variables to MSBuild as items.
/// Environment variables specified via <c>dotnet run -e NAME=VALUE</c> are passed
/// as <c>&lt;RuntimeEnvironmentVariable Include="NAME" Value="VALUE" /&gt;</c> items.
/// </summary>
internal static class EnvironmentVariablesToMSBuild
{
    private const string PropsFileName = "dotnet-run-env.props";

    /// <summary>
    /// Adds environment variables as MSBuild items to a ProjectInstance.
    /// Use this for in-process MSBuild operations (e.g., DeployToDevice target).
    /// </summary>
    /// <param name="projectInstance">The MSBuild project instance to add items to.</param>
    /// <param name="environmentVariables">The environment variables to add.</param>
    public static void AddAsItems(ProjectInstance projectInstance, IReadOnlyDictionary<string, string> environmentVariables)
    {
        foreach (var (name, value) in environmentVariables)
        {
            projectInstance.AddItem(Constants.RuntimeEnvironmentVariable, name, new Dictionary<string, string>
            {
                ["Value"] = value
            });
        }
    }

    /// <summary>
    /// Creates a temporary .props file containing environment variables as MSBuild items.
    /// Use this for out-of-process MSBuild operations where you need to inject items via
    /// <c>CustomBeforeMicrosoftCommonProps</c> property.
    /// </summary>
    /// <param name="projectFilePath">The full path to the project file. If null or empty, returns null.</param>
    /// <param name="environmentVariables">The environment variables to include.</param>
    /// <param name="intermediateOutputPath">
    /// Optional intermediate output path where the file will be created.
    /// If null or empty, defaults to "obj" subdirectory of the project directory.
    /// </param>
    /// <returns>The full path to the created props file, or null if no environment variables were specified or projectFilePath is null.</returns>
    public static string? CreatePropsFile(string? projectFilePath, IReadOnlyDictionary<string, string> environmentVariables, string? intermediateOutputPath = null)
    {
        if (string.IsNullOrEmpty(projectFilePath) || environmentVariables.Count == 0)
        {
            return null;
        }

        string projectDirectory = Path.GetDirectoryName(projectFilePath) ?? "";
        
        // Normalize path separators - MSBuild may return paths with backslashes on non-Windows
        string normalized = intermediateOutputPath?.Replace('\\', Path.DirectorySeparatorChar) ?? "";
        string objDir = string.IsNullOrEmpty(normalized)
            ? Path.Combine(projectDirectory, Constants.ObjDirectoryName)
            : Path.IsPathRooted(normalized)
                ? normalized
                : Path.Combine(projectDirectory, normalized);
        Directory.CreateDirectory(objDir);

        // Ensure we return a full path for MSBuild property usage
        string propsFilePath = Path.GetFullPath(Path.Combine(objDir, PropsFileName));
        using (var stream = File.Create(propsFilePath))
        {
            WritePropsFileContent(stream, environmentVariables);
        }

        return propsFilePath;
    }

    /// <summary>
    /// Deletes the temporary environment variables props file if it exists.
    /// </summary>
    /// <param name="propsFilePath">The path to the props file to delete.</param>
    public static void DeletePropsFile(string? propsFilePath)
    {
        if (propsFilePath is not null && File.Exists(propsFilePath))
        {
            try
            {
                File.Delete(propsFilePath);
            }
            catch (Exception ex)
            {
                // Best effort cleanup - don't fail the build if we can't delete the temp file
                Reporter.Verbose.WriteLine($"Failed to delete temporary props file '{propsFilePath}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Adds the props file property to the MSBuild arguments.
    /// This uses <c>CustomBeforeMicrosoftCommonProps</c> to inject the props file early in evaluation.
    /// </summary>
    /// <param name="msbuildArgs">The base MSBuild arguments.</param>
    /// <param name="propsFilePath">The path to the props file (from <see cref="CreatePropsFile"/>).</param>
    /// <returns>The MSBuild arguments with the props file property added, or the original args if propsFilePath is null.</returns>
    public static MSBuildArgs AddPropsFileToArgs(MSBuildArgs msbuildArgs, string? propsFilePath)
    {
        if (propsFilePath is null)
        {
            return msbuildArgs;
        }

        // Add the props file via CustomBeforeMicrosoftCommonProps.
        // This ensures the items are available early in evaluation, similar to how we add items
        // directly to ProjectInstance for in-process target invocations.
        var additionalProperties = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
        {
            [Constants.CustomBeforeMicrosoftCommonProps] = propsFilePath
        });

        return msbuildArgs.CloneWithAdditionalProperties(additionalProperties);
    }

    /// <summary>
    /// Writes the content of the .props file containing environment variables as items.
    /// </summary>
    private static void WritePropsFileContent(Stream stream, IReadOnlyDictionary<string, string> environmentVariables)
    {
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true
        });

        writer.WriteStartElement("Project");
        writer.WriteStartElement("ItemGroup");

        foreach (var (name, value) in environmentVariables)
        {
            writer.WriteStartElement(Constants.RuntimeEnvironmentVariable);
            writer.WriteAttributeString("Include", name);
            writer.WriteAttributeString("Value", value);
            writer.WriteEndElement();
        }

        writer.WriteEndElement(); // ItemGroup
        writer.WriteEndElement(); // Project
    }
}
