// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;
using Moq;

namespace Microsoft.DotNet.ApiDiff.Tests;

public class DiffDiskTests
{
    private const string BeforeFolderName = "Before";
    private const string AfterFolderName = "After";
    private const string TableOfContentsTitle = "MyTitle";
    private const string ActualAssemblyName = "MyAssembly";

    private const string ExpectedTableOfContents = $"""
        # API difference between {BeforeFolderName} and {AfterFolderName}

        API listing follows standard diff formatting.
        Lines preceded by a '+' are additions and a '-' indicates removal.

        * [{ActualAssemblyName}]({TableOfContentsTitle}_{ActualAssemblyName}.md)
    """;

    private const string BeforeCode = """
        namespace MyNamespace
        {
            public class MyClass
            {
                public MyClass() { }
            }
        }
        """;

    private const string AfterCode = """
        namespace MyNamespace
        {
            public class MyClass
            {
                public MyClass() { }
                public void MyMethod() { }
            }
        }
        """;
    private const string ExpectedMarkdown = $@"# {ActualAssemblyName}

```diff
  namespace MyNamespace
  {{
      public class MyClass
      {{
+         public void MyMethod() {{ }}
      }}
  }}
```
";

    /// <summary>
    /// This test reads two DLLs, where the only difference between the types in the assembly is one method that is added on the new type.
    /// The output goes to disk.
    /// </summary>
    [Fact]
    public void TestDiskReadAndWrite()
    {
        using TempDirectory inputFolderPath = new();
        using TempDirectory outputFolderPath = new();

        IDiffGenerator generator = TestDiskShared(inputFolderPath.DirPath, outputFolderPath.DirPath, writeToDisk: true);
        generator.Run();

        string tableOfContentsMarkdownFilePath = Path.Join(outputFolderPath.DirPath, $"{TableOfContentsTitle}.md");
        Assert.True(File.Exists(tableOfContentsMarkdownFilePath), $"{tableOfContentsMarkdownFilePath} table of contents markdown file does not exist.");

        string myAssemblyMarkdownFilePath = Path.Join(outputFolderPath.DirPath, $"{TableOfContentsTitle}_{ActualAssemblyName}.md");
        Assert.True(File.Exists(myAssemblyMarkdownFilePath), $"{myAssemblyMarkdownFilePath} assembly markdown file does not exist.");

        string actualCode = File.ReadAllText(myAssemblyMarkdownFilePath);
        Assert.Equal(ExpectedMarkdown, actualCode);
    }

    /// <summary>
    /// This test reads two DLLs, where the only difference between the types in the assembly is one method that is added on the new type.
    /// The output is the Results dictionary.
    /// </summary>
    [Fact]
    public void TestDiskReadAndMemoryWrite()
    {
        using TempDirectory inputFolderPath = new();
        using TempDirectory outputFolderPath = new();

        IDiffGenerator generator = TestDiskShared(inputFolderPath.DirPath, outputFolderPath.DirPath, writeToDisk: false);
        generator.Run();

        string tableOfContentsMarkdownFilePath = Path.Join(outputFolderPath.DirPath, $"{TableOfContentsTitle}.md");
        Assert.Contains(tableOfContentsMarkdownFilePath, generator.Results.Keys);

        string myAssemblyMarkdownFilePath = Path.Join(outputFolderPath.DirPath, $"{TableOfContentsTitle}_{ActualAssemblyName}.md");
        Assert.Contains(myAssemblyMarkdownFilePath, generator.Results.Keys);

        Assert.Equal(ExpectedMarkdown, generator.Results[myAssemblyMarkdownFilePath]);
    }

    private IDiffGenerator TestDiskShared(string inputFolderPath, string outputFolderPath, bool writeToDisk)
    {
        string beforeAssembliesFolderPath = Path.Join(inputFolderPath, BeforeFolderName);
        string afterAssembliesFolderPath = Path.Join(inputFolderPath, AfterFolderName);

        Directory.CreateDirectory(beforeAssembliesFolderPath);
        Directory.CreateDirectory(afterAssembliesFolderPath);

        WriteCodeToAssemblyInDisk(beforeAssembliesFolderPath, BeforeCode);
        WriteCodeToAssemblyInDisk(afterAssembliesFolderPath, AfterCode);

        Assert.True(Directory.Exists(beforeAssembliesFolderPath), $"{beforeAssembliesFolderPath} dir does not exist.");
        Assert.True(Directory.Exists(afterAssembliesFolderPath), $"{afterAssembliesFolderPath} dir does not exist.");

        Mock<ILog> log = new();

        return DiffGeneratorFactory.Create(log.Object,
            beforeAssembliesFolderPath,
            beforeAssemblyReferencesFolderPath: null,
            afterAssembliesFolderPath,
            afterAssemblyReferencesFolderPath: null,
            outputFolderPath,
            TableOfContentsTitle,
            attributesToExclude: null,
            addPartialModifier: false,
            hideImplicitDefaultConstructors: true,
            writeToDisk,
            DiffGeneratorFactory.DefaultDiagnosticOptions);
    }

    private void WriteCodeToAssemblyInDisk(string assemblyDirectoryPath, string code)
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);

        IEnumerable<MetadataReference> references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>();

        CSharpCompilation compilation = CSharpCompilation.Create(
            ActualAssemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var assemblyPath = Path.Join(assemblyDirectoryPath, $"{ActualAssemblyName}.dll");
        EmitResult result = compilation.Emit(assemblyPath);

        if (!result.Success)
        {
            IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
            string failureMessages = string.Join(Environment.NewLine, failures.Select(f => $"{f.Id}: {f.GetMessage()}"));
            Assert.Fail($"Compilation failed: {failureMessages}");
        }
    }
}
