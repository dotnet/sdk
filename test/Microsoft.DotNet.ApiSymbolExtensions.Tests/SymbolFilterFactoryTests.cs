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
        Test_FilterFromFiles_Internal(includeCustomType, accessibilitySymbolFilter: null);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Test_FilterFromFiles_CustomAccessibilityFilter(bool includeCustomType)
    {
        Test_FilterFromFiles_Internal(includeCustomType, new AccessibilitySymbolFilter(includeInternalSymbols: true));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Test_FilterFromList(bool includeCustomType)
    {
        Test_FilterFromList_Internal(includeCustomType, accessibilitySymbolFilter: null);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Test_FilterFromList_WithCustomAccessibilityFilter(bool includeCustomType)
    {
        Test_FilterFromList_Internal(includeCustomType, new AccessibilitySymbolFilter(includeInternalSymbols: true));
    }

    private void Test_FilterFromFiles_Internal(bool includeCustomType, AccessibilitySymbolFilter accessibilitySymbolFilter)
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
            accessibilitySymbolFilter: accessibilitySymbolFilter,
            respectInternals: true,
            includeEffectivelyPrivateSymbols: true,
            includeExplicitInterfaceImplementationSymbols: true) as CompositeSymbolFilter;

        Test_GetFilter_Internal(filter, includeCustomType);
    }

    private void Test_FilterFromList_Internal(bool includeCustomType, AccessibilitySymbolFilter accessibilitySymbolFilter)
    {
        List<string> exclusions = ["T:System.Int32", "T:System.String"];
        if (!includeCustomType)
        {
            exclusions.Add("T:MyNamespace.MyClass");
        }

        CompositeSymbolFilter filter = SymbolFilterFactory.GetFilterFromList(
            apiExclusionList: exclusions.ToArray(),
            accessibilitySymbolFilter: accessibilitySymbolFilter,
            respectInternals: true,
            includeEffectivelyPrivateSymbols: true,
            includeExplicitInterfaceImplementationSymbols: true) as CompositeSymbolFilter;

        Test_GetFilter_Internal(filter, includeCustomType);
    }

    private void Test_GetFilter_Internal(CompositeSymbolFilter compositeFilter, bool includeCustomType)
    {
        Assert.NotNull(compositeFilter);

        Assert.Equal(3, compositeFilter.Filters.Count);

        DocIdSymbolFilter docIdFilter = compositeFilter.Filters[0] as DocIdSymbolFilter;
        Assert.NotNull(docIdFilter);

        ImplicitSymbolFilter implicitFilter = compositeFilter.Filters[1] as ImplicitSymbolFilter;
        Assert.NotNull(implicitFilter);

        AccessibilitySymbolFilter accessibilityFilter = compositeFilter.Filters[2] as AccessibilitySymbolFilter;
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
