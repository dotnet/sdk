// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;
using Moq;

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffDiskTests
{
    private readonly string ExpectedCode = """
        # MyAssembly

        ```diff
          namespace MyNamespace
          {
              public class MyClass
              {
        +         public void MyMethod() { }
              }
          }
        ```

        """;

    /// <summary>
    /// This test reads two DLLs, where the only difference is a method added on the second one.
    /// </summary>
    [Fact]
    public void TestDiskReadAndWrite()
    {
        using TempDirectory outputFolderPath = new();

        string beforeAssembliesFolderPath = Path.Join(AppContext.BaseDirectory, "TestAssemblies", "Before");
        string afterAssembliesFolderPath = Path.Join(AppContext.BaseDirectory, "TestAssemblies", "After");

        Assert.True(Directory.Exists(beforeAssembliesFolderPath), $"{beforeAssembliesFolderPath} dir does not exist.");
        Assert.True(Directory.Exists(afterAssembliesFolderPath), $"{afterAssembliesFolderPath} dir does not exist.");

        Mock<ILog> log = new();

        DiffGenerator.RunAndSaveToDisk(log.Object,
            outputFolderPath.DirPath,
            attributesToExclude: null,
            beforeAssembliesFolderPath,
            beforeAssemblyReferencesFolderPath: null,
            afterAssembliesFolderPath,
            afterAssemblyReferencesFolderPath: null,
            addPartialModifier: false,
            hideImplicitDefaultConstructors: true,
            DiffGenerator.DefaultDiagnosticOptions);

        string outputFilePath = Path.Join(outputFolderPath.DirPath, "MyAssembly.md");
        Assert.True(File.Exists(outputFilePath), $"{outputFilePath} markdown file does not exist.");

        string actualCode = File.ReadAllText(outputFilePath);

        Assert.Equal(ExpectedCode, actualCode);
    }

}
