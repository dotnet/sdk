// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Xunit;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiSymbolExtensions.Tests;

public class SymbolFilterFactoryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Test_FilterFromFiles(bool includeCustomType)
    {
        using TempDirectory root = new();
        string filePath = Path.Combine(root.DirPath, "exclusions.txt");
        using (FileStream fileStream = File.Create(filePath))
        {
            using StreamWriter writer = new(fileStream);
            writer.WriteLine("T:System.Int32");
            writer.WriteLine("T:System.String");
            if (!includeCustomType)
            {
                writer.WriteLine("T:MyNamespace.MyClass");
            }
        }

        CompositeSymbolFilter filter = SymbolFilterFactory.GetFilterFromFiles(
            apiExclusionFilePaths: [filePath],
            respectInternals: true,
            includeEffectivelyPrivateSymbols: true,
            includeExplicitInterfaceImplementationSymbols: true) as CompositeSymbolFilter;

        Test_GetFilter_Internal(filter, includeCustomType);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Test_FilterFromList(bool includeCustomType)
    {
        List<string> exclusions = ["T:System.Int32", "T:System.String"];
        if (!includeCustomType)
        {
            exclusions.Add("T:MyNamespace.MyClass");
        }

        CompositeSymbolFilter filter = SymbolFilterFactory.GetFilterFromList(
            apiExclusionList: exclusions.ToArray(),
            respectInternals: true,
            includeEffectivelyPrivateSymbols: true,
            includeExplicitInterfaceImplementationSymbols: true) as CompositeSymbolFilter;

        Test_GetFilter_Internal(filter, includeCustomType);
    }

    private void Test_GetFilter_Internal(CompositeSymbolFilter filter, bool includeCustomType)
    {
        Assert.NotNull(filter);

        Assert.Equal(3, filter.Filters.Count);

        DocIdSymbolFilter docIdFilter = filter.Filters[0] as DocIdSymbolFilter;
        Assert.NotNull(docIdFilter);

        ImplicitSymbolFilter implicitFilter = filter.Filters[1] as ImplicitSymbolFilter;
        Assert.NotNull(implicitFilter);

        AccessibilitySymbolFilter accessibilityFilter = filter.Filters[2] as AccessibilitySymbolFilter;
        Assert.NotNull(accessibilityFilter);

        IAssemblySymbol assemblySymbol = SymbolFactory.GetAssemblyFromSyntax(@"
namespace MyNamespace
{
    public class MyClass { }
}");
        Assert.NotNull(assemblySymbol);
        INamedTypeSymbol myClass = assemblySymbol.GetTypeByMetadataName("MyNamespace.MyClass");
        Assert.NotNull(myClass);
        Assert.Equal(includeCustomType, docIdFilter.Include(myClass));
    }
}
