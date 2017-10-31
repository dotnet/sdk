// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using System.Xml.Linq;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPreserveCompilationContext : SdkTest
    {
        public GivenThatWeWantToPreserveCompilationContext(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_publishes_the_project_with_a_refs_folder_and_correct_deps_file()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("CompilationContext", "PreserveCompilationContext")
                .WithSource();

            testAsset.Restore(Log, "TestApp");
            testAsset.Restore(Log, "TestLibrary");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            foreach (var targetFramework in new[] { "net46", "netcoreapp1.1" })
            {
                var publishCommand = new PublishCommand(Log, appProjectDirectory);

                if (targetFramework == "net46" && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    continue;
                }

                publishCommand
                    .Execute($"/p:TargetFramework={targetFramework}")
                    .Should()
                    .Pass();

                var publishDirectory = publishCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: "win7-x86");

                publishDirectory.Should().HaveFiles(new[] {
                    targetFramework == "net46" ? "TestApp.exe" : "TestApp.dll",
                    "TestLibrary.dll",
                    "Newtonsoft.Json.dll"});

                var refsDirectory = new DirectoryInfo(Path.Combine(publishDirectory.FullName, "refs"));
                // Should have compilation time assemblies
                refsDirectory.Should().HaveFile("System.IO.dll");
                // Libraries in which lib==ref should be deduped
                refsDirectory.Should().NotHaveFile("TestLibrary.dll");
                refsDirectory.Should().NotHaveFile("Newtonsoft.Json.dll");

                using (var depsJsonFileStream = File.OpenRead(Path.Combine(publishDirectory.FullName, "TestApp.deps.json")))
                {
                    var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);

                    string[] expectedDefines;
                    if (targetFramework == "net46")
                    {
                        expectedDefines = new[] { "DEBUG", "TRACE", "NET46" };
                    }
                    else
                    {
                        expectedDefines = new[] { "DEBUG", "TRACE", "NETCOREAPP1_1" };
                    }

                    dependencyContext.CompilationOptions.Defines.Should().BeEquivalentTo(expectedDefines);
                    dependencyContext.CompilationOptions.LanguageVersion.Should().Be("");
                    dependencyContext.CompilationOptions.Platform.Should().Be("x86");
                    dependencyContext.CompilationOptions.Optimize.Should().Be(false);
                    dependencyContext.CompilationOptions.KeyFile.Should().Be("");
                    dependencyContext.CompilationOptions.EmitEntryPoint.Should().Be(true);
                    dependencyContext.CompilationOptions.DebugType.Should().Be("portable");

                    var compileLibraryAssemblyNames = dependencyContext.CompileLibraries.SelectMany(cl => cl.Assemblies)
                        .Select(a => a.Split('/').Last())
                        .Distinct().ToList();
                    var expectedCompileLibraryNames = targetFramework == "net46" ? Net46CompileLibraryNames.Distinct() : NetCoreAppCompileLibraryNames.Distinct();

                    var extraCompileLibraryNames = compileLibraryAssemblyNames.Except(expectedCompileLibraryNames).ToList();

                    compileLibraryAssemblyNames.Should().BeEquivalentTo(expectedCompileLibraryNames);

                    // Ensure P2P references are specified correctly
                    var testLibrary = dependencyContext
                        .CompileLibraries
                        .FirstOrDefault(l => string.Equals(l.Name, "testlibrary", StringComparison.OrdinalIgnoreCase));

                    testLibrary.Assemblies.Count.Should().Be(1);
                    testLibrary.Assemblies[0].Should().Be("TestLibrary.dll");

                    // Ensure framework references are specified correctly
                    if (targetFramework == "net46")
                    {
                        var mscorlibLibrary = dependencyContext
                            .CompileLibraries
                            .FirstOrDefault(l => string.Equals(l.Name, "mscorlib", StringComparison.OrdinalIgnoreCase));
                        mscorlibLibrary.Assemblies.Count.Should().Be(1);
                        mscorlibLibrary.Assemblies[0].Should().Be(".NETFramework/v4.6/mscorlib.dll");

                        var systemCoreLibrary = dependencyContext
                            .CompileLibraries
                            .FirstOrDefault(l => string.Equals(l.Name, "system.core", StringComparison.OrdinalIgnoreCase));
                        systemCoreLibrary.Assemblies.Count.Should().Be(1);
                        systemCoreLibrary.Assemblies[0].Should().Be(".NETFramework/v4.6/System.Core.dll");

                        var systemCollectionsLibrary = dependencyContext
                            .CompileLibraries
                            .FirstOrDefault(l => string.Equals(l.Name, "system.collections.reference", StringComparison.OrdinalIgnoreCase));
                        systemCollectionsLibrary.Assemblies.Count.Should().Be(1);
                        systemCollectionsLibrary.Assemblies[0].Should().Be(".NETFramework/v4.6/Facades/System.Collections.dll");
                    }
                }
            }
        }

        [Fact]
        public void It_excludes_runtime_store_packages_from_the_refs_folder()
        {
            var targetFramework = "netcoreapp2.0";

            var testAsset = _testAssetsManager
                .CopyTestAsset("CompilationContext", "PreserveCompilationContextRefs")
                .WithSource()
                .WithProjectChanges((path, project) =>
                {
                    if (Path.GetFileName(path).Equals("TestApp.csproj", StringComparison.OrdinalIgnoreCase))
                    {
                        var ns = project.Root.Name.Namespace;

                        var targetFrameworkElement = project.Root.Elements(ns + "PropertyGroup").Elements(ns + "TargetFrameworks").Single();
                        targetFrameworkElement.SetValue(targetFramework);
                    }
                })
                .Restore(Log, "TestApp");

            var manifestFile = Path.Combine(testAsset.TestRoot, "manifest.xml");

            File.WriteAllLines(manifestFile, new[]
            {
                "<StoreArtifacts>",
                @"  <Package Id=""Newtonsoft.Json"" Version=""9.0.1"" />",
                @"  <Package Id=""System.Data.SqlClient"" Version=""4.3.0"" />",
                "</StoreArtifacts>",
            });

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            var publishCommand = new PublishCommand(Log, appProjectDirectory);
            publishCommand
                .Execute($"/p:TargetFramework={targetFramework}", $"/p:TargetManifestFiles={manifestFile}")
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: "win7-x86");

            publishDirectory.Should().HaveFiles(new[] {
                "TestApp.dll",
                "TestLibrary.dll"});

            // excluded through TargetManifestFiles
            publishDirectory.Should().NotHaveFile("Newtonsoft.Json.dll");
            publishDirectory.Should().NotHaveFile("System.Data.SqlClient.dll");

            var refsDirectory = new DirectoryInfo(Path.Combine(publishDirectory.FullName, "refs"));
            // Should have compilation time assemblies
            refsDirectory.Should().HaveFile("System.IO.dll");
            // System.Data.SqlClient has separate compile and runtime assemblies, so the compile assembly
            // should be copied to refs even though the runtime assembly is listed in TargetManifestFiles
            refsDirectory.Should().HaveFile("System.Data.SqlClient.dll");

            // Libraries in which lib==ref should be deduped
            refsDirectory.Should().NotHaveFile("TestLibrary.dll");
            refsDirectory.Should().NotHaveFile("Newtonsoft.Json.dll");
        }

        string[] Net46CompileLibraryNames = @"TestApp.exe
mscorlib.dll
System.ComponentModel.Composition.dll
System.Core.dll
System.Data.Common.dll
System.Data.dll
System.Data.SqlClient.dll
System.dll
System.Drawing.dll
System.IO.Compression.FileSystem.dll
System.Numerics.dll
System.Runtime.Serialization.dll
System.Xml.dll
System.Xml.Linq.dll
System.Collections.Concurrent.dll
System.Collections.dll
System.ComponentModel.Annotations.dll
System.ComponentModel.dll
System.ComponentModel.EventBasedAsync.dll
System.Diagnostics.Contracts.dll
System.Diagnostics.Debug.dll
System.Diagnostics.Tools.dll
System.Diagnostics.Tracing.dll
System.Dynamic.Runtime.dll
System.Globalization.dll
System.IO.dll
System.Linq.dll
System.Linq.Expressions.dll
System.Linq.Parallel.dll
System.Linq.Queryable.dll
System.Net.NetworkInformation.dll
System.Net.Primitives.dll
System.Net.Requests.dll
System.Net.WebHeaderCollection.dll
System.ObjectModel.dll
System.Reflection.dll
System.Reflection.Emit.dll
System.Reflection.Emit.ILGeneration.dll
System.Reflection.Emit.Lightweight.dll
System.Reflection.Extensions.dll
System.Reflection.Primitives.dll
System.Resources.ResourceManager.dll
System.Runtime.dll
System.Runtime.Extensions.dll
System.Runtime.Handles.dll
System.Runtime.InteropServices.dll
System.Runtime.InteropServices.WindowsRuntime.dll
System.Runtime.Numerics.dll
System.Runtime.Serialization.Json.dll
System.Runtime.Serialization.Primitives.dll
System.Runtime.Serialization.Xml.dll
System.Security.Principal.dll
System.ServiceModel.Duplex.dll
System.ServiceModel.Http.dll
System.ServiceModel.NetTcp.dll
System.ServiceModel.Primitives.dll
System.ServiceModel.Security.dll
System.Text.Encoding.dll
System.Text.Encoding.Extensions.dll
System.Text.RegularExpressions.dll
System.Threading.dll
System.Threading.Tasks.dll
System.Threading.Tasks.Parallel.dll
System.Threading.Timer.dll
System.Xml.XDocument.dll
System.Xml.XmlSerializer.dll
Microsoft.Win32.Primitives.dll
Newtonsoft.Json.dll
System.AppContext.dll
System.Console.dll
System.Globalization.Calendars.dll
System.IO.Compression.dll
System.IO.Compression.ZipFile.dll
System.IO.FileSystem.dll
System.IO.FileSystem.Primitives.dll
System.Net.Http.dll
System.Net.Sockets.dll
System.Runtime.InteropServices.RuntimeInformation.dll
System.Security.Cryptography.Algorithms.dll
System.Security.Cryptography.Encoding.dll
System.Security.Cryptography.Primitives.dll
System.Security.Cryptography.X509Certificates.dll
System.Xml.ReaderWriter.dll
TestLibrary.dll".Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        string[] NetCoreAppCompileLibraryNames = @"TestApp.dll
Microsoft.CSharp.dll
Microsoft.VisualBasic.dll
Microsoft.Win32.Primitives.dll
Newtonsoft.Json.dll
System.AppContext.dll
System.Buffers.dll
System.Collections.dll
System.Collections.Concurrent.dll
System.Collections.Immutable.dll
System.ComponentModel.dll
System.ComponentModel.Annotations.dll
System.Console.dll
System.Data.Common.dll
System.Data.SqlClient.dll
System.Diagnostics.Debug.dll
System.Diagnostics.DiagnosticSource.dll
System.Diagnostics.Process.dll
System.Diagnostics.Tools.dll
System.Diagnostics.Tracing.dll
System.Dynamic.Runtime.dll
System.Globalization.dll
System.Globalization.Calendars.dll
System.Globalization.Extensions.dll
System.IO.dll
System.IO.Compression.dll
System.IO.Compression.ZipFile.dll
System.IO.FileSystem.dll
System.IO.FileSystem.Primitives.dll
System.IO.FileSystem.Watcher.dll
System.IO.MemoryMappedFiles.dll
System.IO.UnmanagedMemoryStream.dll
System.Linq.dll
System.Linq.Expressions.dll
System.Linq.Parallel.dll
System.Linq.Queryable.dll
System.Net.Http.dll
System.Net.NameResolution.dll
System.Net.Primitives.dll
System.Net.Requests.dll
System.Net.Security.dll
System.Net.Sockets.dll
System.Net.WebHeaderCollection.dll
System.Numerics.Vectors.dll
System.ObjectModel.dll
System.Reflection.dll
System.Reflection.DispatchProxy.dll
System.Reflection.Extensions.dll
System.Reflection.Metadata.dll
System.Reflection.Primitives.dll
System.Reflection.TypeExtensions.dll
System.Resources.Reader.dll
System.Resources.ResourceManager.dll
System.Runtime.dll
System.Runtime.Extensions.dll
System.Runtime.Handles.dll
System.Runtime.InteropServices.dll
System.Runtime.InteropServices.RuntimeInformation.dll
System.Runtime.Numerics.dll
System.Runtime.Serialization.Primitives.dll
System.Security.Claims.dll
System.Security.Cryptography.Algorithms.dll
System.Security.Cryptography.Encoding.dll
System.Security.Cryptography.Primitives.dll
System.Security.Cryptography.X509Certificates.dll
System.Security.Principal.dll
System.Security.Principal.Windows.dll
System.Text.Encoding.dll
System.Text.Encoding.Extensions.dll
System.Text.RegularExpressions.dll
System.Threading.dll
System.Threading.Overlapped.dll
System.Threading.Tasks.dll
System.Threading.Tasks.Dataflow.dll
System.Threading.Tasks.Extensions.dll
System.Threading.Tasks.Parallel.dll
System.Threading.Thread.dll
System.Threading.ThreadPool.dll
System.Threading.Timer.dll
System.Xml.ReaderWriter.dll
System.Xml.XDocument.dll
TestLibrary.dll".Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }
}