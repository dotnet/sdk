// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.DotNet.Cli.Resources.Generator.Tests;

internal sealed class OracleGenerator : IDisposable
{
    private readonly AssemblyLoadContext _loadContext;

    private OracleGenerator(AssemblyLoadContext loadContext, IIncrementalGenerator generator)
    {
        _loadContext = loadContext;
        Generator = generator;
    }

    internal IIncrementalGenerator Generator { get; }

    internal static OracleGenerator Load()
    {
        string analyzerPath = Path.Combine(
            AppContext.BaseDirectory,
            "TestAssets",
            "ResxSourceGenerator");
        AssemblyLoadContext loadContext = new(
            "Microsoft.DotNet.Cli.Resources.Generator.Oracle." + Guid.NewGuid(),
            isCollectible: true);
        loadContext.Resolving += ResolveRoslyn;

        loadContext.LoadFromAssemblyPath(Path.Combine(
            analyzerPath,
            "Microsoft.CodeAnalysis.ResxSourceGenerator.dll"));
        Assembly csharpGenerator = loadContext.LoadFromAssemblyPath(Path.Combine(
            analyzerPath,
            "Microsoft.CodeAnalysis.ResxSourceGenerator.CSharp.dll"));
        Type generatorType = csharpGenerator.GetType(
            "Microsoft.CodeAnalysis.ResxSourceGenerator.CSharp.CSharpResxGenerator",
            throwOnError: false)
            ?? throw new InvalidOperationException("The Microsoft C# RESX generator type was not found.");
        object instance = Activator.CreateInstance(generatorType, nonPublic: true)
            ?? throw new InvalidOperationException("The Microsoft C# RESX generator could not be created.");

        if (instance is not IIncrementalGenerator generator)
        {
            throw new InvalidOperationException("The Microsoft C# RESX generator uses an incompatible Roslyn API.");
        }

        return new(loadContext, generator);
    }

    public void Dispose()
    {
        _loadContext.Resolving -= ResolveRoslyn;
        _loadContext.Unload();
    }

    private static Assembly? ResolveRoslyn(AssemblyLoadContext context, AssemblyName name)
    {
        if (name.Name == typeof(Compilation).Assembly.GetName().Name)
        {
            return typeof(Compilation).Assembly;
        }

        if (name.Name == typeof(Microsoft.CodeAnalysis.CSharp.CSharpCompilation).Assembly.GetName().Name)
        {
            return typeof(Microsoft.CodeAnalysis.CSharp.CSharpCompilation).Assembly;
        }

        return null;
    }
}