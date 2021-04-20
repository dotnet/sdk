// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotAddArchiveItemPathToTheTargetFileSystemPathTests : TaintedDataAnalyzerTestBase<DoNotAddArchiveItemPathToTheTargetFileSystemPath, DoNotAddArchiveItemPathToTheTargetFileSystemPath>
    {
        protected override DiagnosticDescriptor Rule => DoNotAddArchiveItemPathToTheTargetFileSystemPath.Rule;

        protected override IEnumerable<string> AdditionalCSharpSources => new string[] { zipArchiveEntryAndZipFileExtensionsCSharpSourceCode };

        public const string zipArchiveEntryAndZipFileExtensionsCSharpSourceCode = @"
namespace System.IO.Compression
{
    public class ZipArchiveEntry
    {
        public string FullName { get; }
    }

    public static class ZipFileExtensions
    {
        public static void ExtractToFile (this ZipArchiveEntry source, string destinationFileName)
        {
        }
    }
}";

        [Fact]
        public async Task Test_Sink_ZipArchiveEntry_ExtractToFile_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.IO.Compression;

class TestClass
{
    public void TestMethod(ZipArchiveEntry zipArchiveEntry)
    {
        zipArchiveEntry.ExtractToFile(zipArchiveEntry.FullName);
    }
}",
            GetCSharpResultAt(8, 9, 8, 39, "void ZipFileExtensions.ExtractToFile(ZipArchiveEntry source, string destinationFileName)", "void TestClass.TestMethod(ZipArchiveEntry zipArchiveEntry)", "string ZipArchiveEntry.FullName", "void TestClass.TestMethod(ZipArchiveEntry zipArchiveEntry)"));
        }

        [Fact]
        public async Task Test_Sink_File_Open_WithStringAndFileModeParameters_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.IO;
using System.IO.Compression;

class TestClass
{
    public void TestMethod(ZipArchiveEntry zipArchiveEntry, FileMode mode)
    {
        File.Open(zipArchiveEntry.FullName, mode);
    }
}",
            GetCSharpResultAt(9, 9, 9, 19, "FileStream File.Open(string path, FileMode mode)", "void TestClass.TestMethod(ZipArchiveEntry zipArchiveEntry, FileMode mode)", "string ZipArchiveEntry.FullName", "void TestClass.TestMethod(ZipArchiveEntry zipArchiveEntry, FileMode mode)"));
        }

        [Fact]
        public async Task Test_Sink_File_Open_WithStringAndFileModeAndFileAccessParameters_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.IO;
using System.IO.Compression;

class TestClass
{
    public void TestMethod(ZipArchiveEntry zipArchiveEntry, FileMode mode, FileAccess access)
    {
        File.Open(zipArchiveEntry.FullName, mode, access);
    }
}",
            GetCSharpResultAt(9, 9, 9, 19, "FileStream File.Open(string path, FileMode mode, FileAccess access)", "void TestClass.TestMethod(ZipArchiveEntry zipArchiveEntry, FileMode mode, FileAccess access)", "string ZipArchiveEntry.FullName", "void TestClass.TestMethod(ZipArchiveEntry zipArchiveEntry, FileMode mode, FileAccess access)"));
        }

        [Fact]
        public async Task Test_Sink_File_Open_WithStringAndFileModeAndFileAccessAndFileShareParamters_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.IO;
using System.IO.Compression;

class TestClass
{
    public void TestMethod(ZipArchiveEntry zipArchiveEntry, FileMode mode, FileAccess access, FileShare share)
    {
        File.Open(zipArchiveEntry.FullName, mode, access, share);
    }
}",
            GetCSharpResultAt(9, 9, 9, 19, "FileStream File.Open(string path, FileMode mode, FileAccess access, FileShare share)", "void TestClass.TestMethod(ZipArchiveEntry zipArchiveEntry, FileMode mode, FileAccess access, FileShare share)", "string ZipArchiveEntry.FullName", "void TestClass.TestMethod(ZipArchiveEntry zipArchiveEntry, FileMode mode, FileAccess access, FileShare share)"));
        }

        [Fact]
        public async Task Test_Sink_FileStream_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.IO;
using System.IO.Compression;

class TestClass
{
    public void TestMethod(ZipArchiveEntry zipArchiveEntry, FileMode mode)
    {
        var fileStream = new FileStream(zipArchiveEntry.FullName, mode);
    }
}",
            GetCSharpResultAt(9, 26, 9, 41, "FileStream.FileStream(string path, FileMode mode)", "void TestClass.TestMethod(ZipArchiveEntry zipArchiveEntry, FileMode mode)", "string ZipArchiveEntry.FullName", "void TestClass.TestMethod(ZipArchiveEntry zipArchiveEntry, FileMode mode)"));
        }

        [Fact]
        public async Task Test_Sink_FileInfo_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.IO;
using System.IO.Compression;

class TestClass
{
    public void TestMethod(ZipArchiveEntry zipArchiveEntry)
    {
        var fileInfo = new FileInfo(zipArchiveEntry.FullName);
    }
}",
            GetCSharpResultAt(9, 24, 9, 37, "FileInfo.FileInfo(string fileName)", "void TestClass.TestMethod(ZipArchiveEntry zipArchiveEntry)", "string ZipArchiveEntry.FullName", "void TestClass.TestMethod(ZipArchiveEntry zipArchiveEntry)"));
        }

        [Fact]
        public async Task Test_Sanitizer_String_StartsWith_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.IO.Compression;

class TestClass
{
    public void TestMethod(ZipArchiveEntry zipArchiveEntry)
    {
        var destinationFileName = zipArchiveEntry.FullName;

        if(destinationFileName.StartsWith(""Start""))
        {
            zipArchiveEntry.ExtractToFile(destinationFileName);
        }
    }
}");
        }

        [Fact]
        public async Task Test_Sink_ZipArchiveEntry_ExtractToFile_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.IO.Compression;

class TestClass
{
    public void TestMethod(ZipArchiveEntry zipArchiveEntry, string destinationFileName)
    {
        zipArchiveEntry.ExtractToFile(destinationFileName);
    }
}");
        }

        [Fact]
        public async Task Test_Sanitizer_Path_GetFileName_NoDiagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System.IO;
using System.IO.Compression;

class TestClass
{
    public void TestMethod(ZipArchiveEntry zipArchiveEntry)
    {
        var destinationFileName = Path.GetFileName(zipArchiveEntry.FullName);
        zipArchiveEntry.ExtractToFile(destinationFileName);
    }
}");
        }

        [Fact]
        public async Task Test_Sanitizer_String_Substring_Diagnostic()
        {
            await VerifyCSharpWithDependenciesAsync(@"
using System;
using System.IO.Compression;

class TestClass
{
    public void TestMethod(ZipArchiveEntry zipArchiveEntry)
    {
        var destinationFileName = zipArchiveEntry.FullName;
        zipArchiveEntry.ExtractToFile(destinationFileName.Substring(1));
    }
}");
        }

        [Theory]
        [InlineData("")]
        [InlineData("dotnet_code_quality.excluded_symbol_names = TestMethod")]
        [InlineData("dotnet_code_quality.CA5389.excluded_symbol_names = TestMethod")]
        [InlineData("dotnet_code_quality.CA5389.excluded_symbol_names = TestMet*")]
        [InlineData("dotnet_code_quality.dataflow.excluded_symbol_names = TestMethod")]
        public async Task EditorConfigConfiguration_ExcludedSymbolNamesWithValueOption(string editorConfigText)
        {
            var expected = Array.Empty<DiagnosticResult>();
            if (editorConfigText.Length == 0)
            {
                expected = new DiagnosticResult[]
                {
                    GetCSharpResultAt(8, 9, 8, 39, "void ZipFileExtensions.ExtractToFile(ZipArchiveEntry source, string destinationFileName)", "void TestClass.TestMethod(ZipArchiveEntry zipArchiveEntry)", "string ZipArchiveEntry.FullName", "void TestClass.TestMethod(ZipArchiveEntry zipArchiveEntry)")
                };
            }

            await VerifyCSharpWithDependenciesAsync(@"
using System.IO.Compression;

class TestClass
{
    public void TestMethod(ZipArchiveEntry zipArchiveEntry)
    {
        zipArchiveEntry.ExtractToFile(zipArchiveEntry.FullName);
    }
}", ("/.editorconfig", $@"root = true

[*]
{editorConfigText}
"), expected);
        }
    }
}
