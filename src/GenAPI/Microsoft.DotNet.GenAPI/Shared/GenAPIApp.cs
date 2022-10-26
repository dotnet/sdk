// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Class to standertize initilization and running of GenAPI tool.
///     Shared between CLI and MSBuild tasks frontends.
/// </summary>
public static class GenAPIApp
{
    public class Context
    {
        /// <summary>
        /// Delimited (',' or ';') set of paths for assemblies or directories to get all assemblies.
        /// </summary>
        public required string Assembly { get; set; }

        /// <summary>
        /// If true, tries to resolve assembly reference.
        /// </summary>
        public bool? ResolveAssemblyReferences { get; set; }

        /// <summary>
        /// Delimited (',' or ';') set of paths to use for resolving assembly references.
        /// </summary>
        public string? LibPath { get; set; }

        /// <summary>
        /// Output path. Default is the console. Can specify an existing directory as well and
        /// then a file will be created for each assembly with the matching name of the assembly.
        /// </summary>
        public string? OutputPath { get; set; }

        /// <summary>
        /// Specify a file with an alternate header content to prepend to output.
        /// </summary>
        public string? HeaderFile { get; set; }

        /// <summary>
        /// Method bodies should throw PlatformNotSupportedException.
        /// </summary>
        public string? ExceptionMessage { get; set; }

        /// <summary>
        /// Indentation size in `IndentationChar`s. Default is 4.
        /// </summary>
        public int IndentationSize { get; set; } = 4;

        /// <summary>
        /// Indentation character: space, tabulation. Default is space.
        /// </summary>
        public char IndentationChar { get; set; } = ' ';

        /// <summary>
        /// Specify a list in the DocId format of which attributes should be excluded from being applied on apis.
        /// </summary>
        public string? ExcludeAttributesList { get; set; }
    }

    /// <summary>
    /// Initialize and run Roslyn-based GenAPI tool.
    /// </summary>
    public static void Run(Context context)
    {
        var loader = new AssemblySymbolLoader(context.ResolveAssemblyReferences ?? false);
        loader.AddReferenceSearchDirectories(SplitPaths(context.LibPath));

        AddReferenceToRuntimeLibraries(loader);

        var intersectionFilter = new IntersectionFilter()
            .Add<FilterOutDelegateMembers>()
            .Add<FilterOutImplicitSymbols>()
            .Add(new AccessibilityFilter(new[] {
                Accessibility.Public,
                Accessibility.ProtectedOrInternal,
                Accessibility.Protected }));

        if (context.ExcludeAttributesList != null)
        {
            intersectionFilter.Add(new FilterOutAttributes(context.ExcludeAttributesList));
        }

        var assemblySymbols = loader.LoadAssemblies(SplitPaths(context.Assembly));
        foreach (var assemblySymbol in assemblySymbols)
        {
            using var writer = new CSharpBuilder(
                new AssemblySymbolOrderProvider(),
                intersectionFilter,
                new CSharpSyntaxWriter(
                    GetTextWriter(context.OutputPath, assemblySymbol.Name),
                    ReadHeaderFile(context.HeaderFile),
                    context.ExceptionMessage,
                    context.IndentationSize,
                    context.IndentationChar)); 

            writer.WriteAssembly(assemblySymbol);
        }

        foreach (var warn in loader.GetResolutionWarnings())
        {
            Console.WriteLine(warn);
        }
    }

    /// <summary>
    /// Creates a TextWriter capable to write into Console or cs file.
    /// </summary>
    /// <param name="outputDirPath">Path to a directory where file with `assemblyName`.cs filename needs to be created.
    ///     If Null - output to Console.Out.</param>
    /// <param name="assemblyName">Name of an assembly. if outputDirPath is not a Null - represents a file name.</param>
    /// <returns></returns>
    private static TextWriter GetTextWriter(string? outputDirPath, string assemblyName)
    {
        if (outputDirPath == null)
        {
            return Console.Out;
        }

        string fileName = assemblyName + ".cs";
        if (Directory.Exists(outputDirPath) && !string.IsNullOrEmpty(fileName))
        {
            return File.CreateText(Path.Combine(outputDirPath, fileName));
        }

        return File.CreateText(outputDirPath);
    }

    /// <summary>
    /// Splits delimiter separated list of pathes represented as a string to a List of paths.
    /// </summary>
    /// <param name="pathSet">Delimiter separated list of paths.</param>
    /// <returns></returns>
    private static string[] SplitPaths(string? pathSet)
    {
        if (pathSet == null) return Array.Empty<string>();

        return pathSet.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Read the header file if specified, or use default one.
    /// </summary>
    /// <param name="headerFile">File with an alternate header content to prepend to output</param>
    /// <returns></returns>
    public static string ReadHeaderFile(string? headerFile)
    {
        const string defaultFileHeader = """
            //------------------------------------------------------------------------------
            // <auto-generated>
            //     This code was generated by a tool.
            //     Roslyn-based GenAPI
            //
            //     Changes to this file may cause incorrect behavior and will be lost if
            //     the code is regenerated.
            // </auto-generated>
            //------------------------------------------------------------------------------
            """;

        if (!string.IsNullOrEmpty(headerFile))
        {
            return File.ReadAllText(headerFile);
        }
        return defaultFileHeader;
    }

    private static void AddReferenceToRuntimeLibraries(IAssemblySymbolLoader loader)
    {
        var corlibLocation = typeof(object).Assembly.Location;
        var runtimeFolder = Path.GetDirectoryName(corlibLocation) ??
            throw new ArgumentNullException("RuntimeFolder", "Could not find path to a runtime folder");

        loader.AddReferenceSearchDirectory(runtimeFolder);
    }
}
