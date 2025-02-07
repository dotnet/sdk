// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    private const string DefaultTableOfContentsTitle = "MyTitle";
    private const string DefaultAssemblyName = "MyAssembly";

    private const string ExpectedTableOfContents = $"""
        # API difference between {BeforeFolderName} and {AfterFolderName}

        API listing follows standard diff formatting.
        Lines preceded by a '+' are additions and a '-' indicates removal.

        * [{DefaultAssemblyName}]({DefaultTableOfContentsTitle}_{DefaultAssemblyName}.md)


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
    private const string DefaultExpectedMarkdown = $@"# {DefaultAssemblyName}

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

    private readonly Dictionary<string, string[]> DefaultBeforeAssemblyAndCodeFiles = new() { { DefaultAssemblyName, [BeforeCode] } };
    private readonly Dictionary<string, string[]> DefaultAfterAssemblyAndCodeFiles = new() { { DefaultAssemblyName, [AfterCode] } };
    private readonly Dictionary<string, string> DefaultExpectedAssemblyMarkdowns = new() { { DefaultAssemblyName, DefaultExpectedMarkdown } };

    /// <summary>
    /// This test reads two DLLs, where the only difference between the types in the assembly is one method that is added on the new type.
    /// The output goes to disk.
    /// </summary>
    [Fact]
    public void Test_DiskRead_DiskWrite()
    {
        using TempDirectory inputFolderPath = new();
        using TempDirectory outputFolderPath = new();

        IDiffGenerator generator = TestDiskShared(inputFolderPath.DirPath, DefaultTableOfContentsTitle, DefaultBeforeAssemblyAndCodeFiles, DefaultAfterAssemblyAndCodeFiles, outputFolderPath.DirPath, writeToDisk: true);
        generator.Run();

        VerifyDiskWrite(outputFolderPath.DirPath, DefaultTableOfContentsTitle, ExpectedTableOfContents, DefaultExpectedAssemblyMarkdowns);
    }

    /// <summary>
    ///  Many namespaces belonging to a single assembly should go into the same output markdown file for that assembly.
    /// </summary>
    [Fact]
    public void Test_DiskRead_DiskWrite_MultiNamespaces()
    {
        using TempDirectory inputFolderPath = new();
        using TempDirectory outputFolderPath = new();

        string beforeCode1 = """
        namespace MyNamespace
        {
            public class MyClass
            {
                public MyClass() { }
            }
        }
        """;

        string beforeCode2 = """
        namespace MyNamespace.MySubNamespace
        {
            public class MySubClass
            {
                public MySubClass() { }
            }
        }
        """;

        string afterCode1 = """
        namespace MyNamespace
        {
            public class MyClass
            {
                public MyClass() { }
                public void MyMethod() { }
            }
        }
        """;

        string afterCode2 = """
        namespace MyNamespace.MySubNamespace
        {
            public class MySubClass
            {
                public MySubClass() { }
                public void MySubMethod() { }
            }
        }
        """;

        string expectedMarkdown = $@"# {DefaultAssemblyName}

```diff
  namespace MyNamespace
  {{
      public class MyClass
      {{
+         public void MyMethod() {{ }}
      }}
  }}
  namespace MyNamespace.MySubNamespace
  {{
      public class MySubClass
      {{
+         public void MySubMethod() {{ }}
      }}
  }}
```
";

        Dictionary<string, string[]> beforeAssemblyAndCodeFiles = new() { { DefaultAssemblyName, [beforeCode1, beforeCode2] } };
        Dictionary<string, string[]> afterAssemblyAndCodeFiles = new() { { DefaultAssemblyName, [afterCode1, afterCode2] } };
        Dictionary<string, string> expectedAssemblyMarkdowns = new() { { DefaultAssemblyName, expectedMarkdown } };

        IDiffGenerator generator = TestDiskShared(inputFolderPath.DirPath, DefaultTableOfContentsTitle, beforeAssemblyAndCodeFiles, afterAssemblyAndCodeFiles, outputFolderPath.DirPath, writeToDisk: true);
        generator.Run();

        VerifyDiskWrite(outputFolderPath.DirPath, DefaultTableOfContentsTitle, ExpectedTableOfContents, expectedAssemblyMarkdowns);
    }

    /// <summary>
    /// This test reads two DLLs, where the only difference between the types in the assembly is one method that is added on the new type.
    /// The output is the Results dictionary.
    /// </summary>
    [Fact]
    public void Test_DiskRead_MemoryWrite()
    {
        using TempDirectory inputFolderPath = new();
        using TempDirectory outputFolderPath = new();

        IDiffGenerator generator = TestDiskShared(inputFolderPath.DirPath, DefaultTableOfContentsTitle, DefaultBeforeAssemblyAndCodeFiles, DefaultAfterAssemblyAndCodeFiles, outputFolderPath.DirPath, writeToDisk: false);
        generator.Run();

        string tableOfContentsMarkdownFilePath = Path.Join(outputFolderPath.DirPath, $"{DefaultTableOfContentsTitle}.md");
        Assert.Contains(tableOfContentsMarkdownFilePath, generator.Results.Keys);

        Assert.Equal(ExpectedTableOfContents, generator.Results[tableOfContentsMarkdownFilePath]);

        string myAssemblyMarkdownFilePath = Path.Join(outputFolderPath.DirPath, $"{DefaultTableOfContentsTitle}_{DefaultAssemblyName}.md");
        Assert.Contains(myAssemblyMarkdownFilePath, generator.Results.Keys);

        Assert.Equal(DefaultExpectedMarkdown, generator.Results[myAssemblyMarkdownFilePath]);
    }

    private IDiffGenerator TestDiskShared(string inputFolderPath, string tableOfContentsTitle, Dictionary<string, string[]> beforeAssemblyAndCodeFiles, Dictionary<string, string[]> afterAssemblyAndCodeFiles, string outputFolderPath, bool writeToDisk)
    {
        string beforeAssembliesFolderPath = Path.Join(inputFolderPath, BeforeFolderName);
        string afterAssembliesFolderPath = Path.Join(inputFolderPath, AfterFolderName);

        Directory.CreateDirectory(beforeAssembliesFolderPath);
        Directory.CreateDirectory(afterAssembliesFolderPath);

        WriteCodeToAssemblyInDisk(beforeAssembliesFolderPath, beforeAssemblyAndCodeFiles);
        WriteCodeToAssemblyInDisk(afterAssembliesFolderPath, afterAssemblyAndCodeFiles);

        Mock<ILog> log = new();

        return DiffGeneratorFactory.Create(log.Object,
            beforeAssembliesFolderPath,
            beforeAssemblyReferencesFolderPath: null,
            afterAssembliesFolderPath,
            afterAssemblyReferencesFolderPath: null,
            outputFolderPath,
            tableOfContentsTitle,
            attributesToExclude: null,
            addPartialModifier: false,
            hideImplicitDefaultConstructors: true,
            writeToDisk,
            DiffGeneratorFactory.DefaultDiagnosticOptions);
    }

    private void WriteCodeToAssemblyInDisk(string assemblyDirectoryPath, Dictionary<string, string[]> assemblyAndCodeFiles)
    {
        foreach ((string assemblyName, string[] codeFiles) in assemblyAndCodeFiles)
        {
            List<SyntaxTree> syntaxTrees = new();
            foreach (string codeFile in codeFiles)
            {
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(codeFile));
            }

            IEnumerable<MetadataReference> references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>();

            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees,
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var assemblyPath = Path.Join(assemblyDirectoryPath, $"{assemblyName}.dll");
            EmitResult result = compilation.Emit(assemblyPath);

            if (!result.Success)
            {
                IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                string failureMessages = string.Join(Environment.NewLine, failures.Select(f => $"{f.Id}: {f.GetMessage()}"));
                Assert.Fail($"Compilation failed: {failureMessages}");
            }
        }
    }

    private void VerifyDiskWrite(string outputFolderPath, string tableOfContentsTitle, string expectedTableOfContents, Dictionary<string, string> expectedAssemblyMarkdowns)
    {
        string tableOfContentsMarkdownFilePath = Path.Join(outputFolderPath, $"{tableOfContentsTitle}.md");
        Assert.True(File.Exists(tableOfContentsMarkdownFilePath), $"{tableOfContentsMarkdownFilePath} table of contents markdown file does not exist.");

        string actualTableOfContentsText = File.ReadAllText(tableOfContentsMarkdownFilePath);
        Assert.Equal(expectedTableOfContents, actualTableOfContentsText);

        foreach ((string expectedAssembly, string expectedMarkdown) in expectedAssemblyMarkdowns)
        {
            string myAssemblyMarkdownFilePath = Path.Join(outputFolderPath, $"{tableOfContentsTitle}_{expectedAssembly}.md");
            Assert.True(File.Exists(myAssemblyMarkdownFilePath), $"{myAssemblyMarkdownFilePath} assembly markdown file does not exist.");

            string actualCode = File.ReadAllText(myAssemblyMarkdownFilePath);
            Assert.Equal(expectedMarkdown, actualCode);
        }
    }
}
