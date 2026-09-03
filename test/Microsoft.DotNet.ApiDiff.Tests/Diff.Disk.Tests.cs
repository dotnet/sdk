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
    private const string DefaultBeforeFriendlyName = "Before";
    private const string DefaultAfterFriendlyName = "After";
    private const string DefaultTableOfContentsTitle = "MyTitle";
    private const string DefaultAssemblyName = "MyAssembly";

    private const string ExpectedTableOfContents = $"""
        # API difference between {DefaultBeforeFriendlyName} and {DefaultAfterFriendlyName}

        API listing follows standard diff formatting.
        Lines preceded by a '+' are additions and a '-' indicates removal.

        * [{DefaultAssemblyName}]({DefaultTableOfContentsTitle}_{DefaultAssemblyName}.md)

        """;

    private const string ExpectedEmptyTableOfContents = $"""
        # API difference between {DefaultBeforeFriendlyName} and {DefaultAfterFriendlyName}

        API listing follows standard diff formatting.
        Lines preceded by a '+' are additions and a '-' indicates removal.


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
+         public void MyMethod();
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
    public async Task DiskRead_DiskWrite()
    {
        using TempDirectory inputFolderPath = new();
        using TempDirectory outputFolderPath = new();

        IDiffGenerator generator = TestDiskShared(
            inputFolderPath.DirPath,
            DefaultBeforeFriendlyName,
            DefaultAfterFriendlyName,
            DefaultTableOfContentsTitle,
            DefaultBeforeAssemblyAndCodeFiles,
            DefaultAfterAssemblyAndCodeFiles,
            outputFolderPath.DirPath,
            writeToDisk: true);

        await generator.RunAsync(CancellationToken.None);

        VerifyDiskWrite(outputFolderPath.DirPath, DefaultTableOfContentsTitle, ExpectedTableOfContents, DefaultExpectedAssemblyMarkdowns);
    }

    // Each assembly should have its own file. Confirm that namespaces spread throughout multiple assemblies does not accidentally overwrite any existing files.
    [Fact]
    public async Task DiskRead_DiskWrite_AssembliesWithRepeatedNamespaces()
    {
        string beforeAssembly1Code1 = """
        namespace MyNamespace
        {
            public class MyClass1
            {
                public MyClass1() { }
            }
        }
        """;
        string beforeAssembly1Code2 = """
        namespace MyNamespace
        {
            public class MyClass2
            {
                public MyClass2() { }
            }
        }
        """;
        string beforeAssembly2Code1 = """
        namespace MyNamespace
        {
            public class MyClass3
            {
                public MyClass3() { }
            }
        }
        """;
        string beforeAssembly2Code2 = """
        namespace MyNamespace
        {
            public class MyClass4
            {
                public MyClass4() { }
            }
        }
        """;

        string afterAssembly1Code1 = """
        namespace MyNamespace
        {
            public class MyClass1
            {
                public MyClass1() { }
                public void MyMethod() { }
            }
        }
        """;
        string afterAssembly1Code2 = """
        namespace MyNamespace
        {
            public class MyClass2
            {
                public MyClass2() { }
                public void MyMethod() { }
            }
        }
        """;
        string afterAssembly2Code1 = """
        namespace MyNamespace
        {
            public class MyClass3
            {
                public MyClass3() { }
                public void MyMethod() { }
            }
        }
        """;
        string afterAssembly2Code2 = """
        namespace MyNamespace
        {
            public class MyClass4
            {
                public MyClass4() { }
                public void MyMethod() { }
            }
        }
        """;

        Dictionary<string, string[]> beforeAssemblyAndCodeFiles = new()
        {
            { "Assembly1", [beforeAssembly1Code1, beforeAssembly1Code2] },
            { "Assembly2", [beforeAssembly2Code1, beforeAssembly2Code2] }
        };

        Dictionary<string, string[]> afterAssemblyAndCodeFiles = new()
        {
            { "Assembly1", [afterAssembly1Code1, afterAssembly1Code2] },
            { "Assembly2", [afterAssembly2Code1, afterAssembly2Code2] }
        };

        Dictionary<string, string> expectedAssemblyMarkdowns = new()
        {
            { "Assembly1", $@"# Assembly1

```diff
  namespace MyNamespace
  {{
      public class MyClass1
      {{
+         public void MyMethod();
      }}
      public class MyClass2
      {{
+         public void MyMethod();
      }}
  }}
```
" },
            { "Assembly2", $@"# Assembly2

```diff
  namespace MyNamespace
  {{
      public class MyClass3
      {{
+         public void MyMethod();
      }}
      public class MyClass4
      {{
+         public void MyMethod();
      }}
  }}
```
" }

        };

        string expectedTableOfContents = $@"# API difference between {DefaultBeforeFriendlyName} and {DefaultAfterFriendlyName}

API listing follows standard diff formatting.
Lines preceded by a '+' are additions and a '-' indicates removal.

* [Assembly1]({DefaultTableOfContentsTitle}_Assembly1.md)
* [Assembly2]({DefaultTableOfContentsTitle}_Assembly2.md)
";

        using TempDirectory inputFolderPath = new();
        using TempDirectory outputFolderPath = new();

        IDiffGenerator generator = TestDiskShared(
            inputFolderPath.DirPath,
            DefaultBeforeFriendlyName,
            DefaultAfterFriendlyName,
            DefaultTableOfContentsTitle,
            beforeAssemblyAndCodeFiles,
            afterAssemblyAndCodeFiles,
            outputFolderPath.DirPath,
            writeToDisk: true);

        await generator.RunAsync(CancellationToken.None);

        VerifyDiskWrite(outputFolderPath.DirPath, DefaultTableOfContentsTitle, expectedTableOfContents, expectedAssemblyMarkdowns);
    }

    /// <summary>
    /// This test reads two DLLs, where the assembly is explicitly excluded.
    /// The output is disk is simply the table of contents markdown file with no assembly list. No other files.
    /// </summary>
    [Fact]
    public async Task DiskRead_DiskWrite_ExcludeAssembly()
    {
        using TempDirectory root = new();

        DirectoryInfo inputFolderPath = new(Path.Join(root.DirPath, "inputFolder"));
        DirectoryInfo outputFolderPath = new(Path.Join(root.DirPath, "outputFolder"));

        inputFolderPath.Create();
        outputFolderPath.Create();

        IDiffGenerator generator = TestDiskShared(
            inputFolderPath.FullName,
            DefaultBeforeFriendlyName,
            DefaultAfterFriendlyName,
            DefaultTableOfContentsTitle,
            DefaultBeforeAssemblyAndCodeFiles,
            DefaultAfterAssemblyAndCodeFiles,
            outputFolderPath.FullName,
            writeToDisk: true,
            filesWithAssembliesToExclude: await GetFileWithListsAsync(root, [DefaultAssemblyName]));

        await generator.RunAsync(CancellationToken.None);

        VerifyDiskWrite(outputFolderPath.FullName, DefaultTableOfContentsTitle, ExpectedEmptyTableOfContents, []);
    }

    /// <summary>
    ///  Many namespaces belonging to a single assembly should go into the same output markdown file for that assembly.
    /// </summary>
    [Fact]
    public async Task DiskRead_DiskWrite_MultiNamespaces()
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
+         public void MyMethod();
      }}
  }}
  namespace MyNamespace.MySubNamespace
  {{
      public class MySubClass
      {{
+         public void MySubMethod();
      }}
  }}
```
";

        Dictionary<string, string[]> beforeAssemblyAndCodeFiles = new() { { DefaultAssemblyName, [beforeCode1, beforeCode2] } };
        Dictionary<string, string[]> afterAssemblyAndCodeFiles = new() { { DefaultAssemblyName, [afterCode1, afterCode2] } };
        Dictionary<string, string> expectedAssemblyMarkdowns = new() { { DefaultAssemblyName, expectedMarkdown } };

        IDiffGenerator generator = TestDiskShared(
            inputFolderPath.DirPath,
            DefaultBeforeFriendlyName,
            DefaultAfterFriendlyName,
            DefaultTableOfContentsTitle,
            beforeAssemblyAndCodeFiles,
            afterAssemblyAndCodeFiles,
            outputFolderPath.DirPath,
            writeToDisk: true);

        await generator.RunAsync(CancellationToken.None);

        VerifyDiskWrite(outputFolderPath.DirPath, DefaultTableOfContentsTitle, ExpectedTableOfContents, expectedAssemblyMarkdowns);
    }

    /// <summary>
    /// This test reads two DLLs, where the only difference between the types in the assembly is one method that is added on the new type.
    /// The output is the Results dictionary.
    /// </summary>
    [Fact]
    public async Task DiskRead_MemoryWrite()
    {
        using TempDirectory inputFolderPath = new();
        using TempDirectory outputFolderPath = new();

        IDiffGenerator generator = TestDiskShared(
            inputFolderPath.DirPath,
            DefaultBeforeFriendlyName,
            DefaultAfterFriendlyName,
            DefaultTableOfContentsTitle,
            DefaultBeforeAssemblyAndCodeFiles,
            DefaultAfterAssemblyAndCodeFiles,
            outputFolderPath.DirPath,
            writeToDisk: false);

        await generator.RunAsync(CancellationToken.None);

        string tableOfContentsMarkdownFilePath = Path.Join(outputFolderPath.DirPath, $"{DefaultTableOfContentsTitle}.md");
        Assert.Contains(tableOfContentsMarkdownFilePath, generator.Results.Keys);

        Assert.Equal(ExpectedTableOfContents, generator.Results[tableOfContentsMarkdownFilePath]);

        string myAssemblyMarkdownFilePath = Path.Join(outputFolderPath.DirPath, $"{DefaultTableOfContentsTitle}_{DefaultAssemblyName}.md");
        Assert.Contains(myAssemblyMarkdownFilePath, generator.Results.Keys);

        Assert.Equal(DefaultExpectedMarkdown, generator.Results[myAssemblyMarkdownFilePath]);
    }

    /// <summary>
    /// This test reads two DLLs, where the assembly is explicitly excluded.
    /// The output is an empty Results dictionary.
    /// </summary>
    [Fact]
    public async Task DiskRead_MemoryWrite_ExcludeAssembly()
    {
        using TempDirectory root = new();

        DirectoryInfo inputFolderPath = new(Path.Join(root.DirPath, "inputFolder"));
        DirectoryInfo outputFolderPath = new(Path.Join(root.DirPath, "outputFolder"));

        inputFolderPath.Create();
        outputFolderPath.Create();

        IDiffGenerator generator = TestDiskShared(
            inputFolderPath.FullName,
            DefaultBeforeFriendlyName,
            DefaultAfterFriendlyName,
            DefaultTableOfContentsTitle,
            DefaultBeforeAssemblyAndCodeFiles,
            DefaultAfterAssemblyAndCodeFiles,
            outputFolderPath.FullName,
            writeToDisk: false,
            filesWithAssembliesToExclude: await GetFileWithListsAsync(root, [DefaultAssemblyName]));

        await generator.RunAsync(CancellationToken.None);

        string tableOfContentsMarkdownFilePath = Path.Join(outputFolderPath.FullName, $"{DefaultTableOfContentsTitle}.md");
        Assert.Contains(tableOfContentsMarkdownFilePath, generator.Results.Keys);

        Assert.Equal(ExpectedEmptyTableOfContents, generator.Results[tableOfContentsMarkdownFilePath]);

        string myAssemblyMarkdownFilePath = Path.Join(outputFolderPath.FullName, $"{DefaultTableOfContentsTitle}_{DefaultAssemblyName}.md");
        Assert.DoesNotContain(myAssemblyMarkdownFilePath, generator.Results.Keys);
    }

    private IDiffGenerator TestDiskShared(
        string inputFolderPath,
        string beforeFriendlyName,
        string afterFriendlyName,
        string tableOfContentsTitle,
        Dictionary<string, string[]> beforeAssemblyAndCodeFiles,
        Dictionary<string, string[]> afterAssemblyAndCodeFiles,
        string outputFolderPath,
        bool writeToDisk,
        FileInfo[]? filesWithAssembliesToExclude = null,
        FileInfo[]? filesWithAttributesToExclude = null,
        FileInfo[]? filesWithAPIsToExclude = null)
    {
        string beforeAssembliesFolderPath = Path.Join(inputFolderPath, DefaultBeforeFriendlyName);
        string afterAssembliesFolderPath = Path.Join(inputFolderPath, DefaultAfterFriendlyName);

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
            beforeFriendlyName,
            afterFriendlyName,
            tableOfContentsTitle,
            filesWithAssembliesToExclude ?? [],
            filesWithAttributesToExclude ?? [],
            filesWithAPIsToExclude ?? [],
            addPartialModifier: false,
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

    private Task<FileInfo[]> GetFileWithListsAsync(TempDirectory root, string[] list) => GetFilesWithListsAsync(root, [list]);

    private async Task<FileInfo[]> GetFilesWithListsAsync(TempDirectory root, List<string[]> lists)
    {
        List<FileInfo> filesWithLists = [];

        foreach (string[] list in lists)
        {
            FileInfo file = new(Path.Join(root.DirPath, Path.GetRandomFileName()));
            await File.WriteAllLinesAsync(file.FullName, list);
            filesWithLists.Add(file);
        }

        return [.. filesWithLists];
    }
}
