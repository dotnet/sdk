// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToPreserveCompilationContextForBuild : SdkTest
    {
        public GivenThatWeWantToPreserveCompilationContextForBuild(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void It_supports_copylocal_false_references()
        {
            var testProject = new TestProject()
            {
                Name = "CopyLocalFalseReferences",
                TargetFrameworks = "net461",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.AdditionalProperties.Add("PreserveCompilationContext", "true");

            var testReference = new TestProject()
            {
                Name = "NetStandardLibrary",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true,
                IsExe = false
            };

            testProject.ReferencedProjects.Add(testReference);

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // The "_ReferenceOnlyAssemblies" should be copied to the 'refs' directory, so they can be resolved at runtime
            outputDirectory.Sub("refs")
                .Should().OnlyHaveFiles(Net461ReferenceOnlyAssemblies);

            using (var depsJsonFileStream = File.OpenRead(Path.Combine(outputDirectory.FullName, $"{testProject.Name}.deps.json")))
            {
                var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);

                var compileLibraryAssemblyNames = dependencyContext.CompileLibraries.SelectMany(cl => cl.Assemblies)
                    .Select(a => a.Split('/').Last())
                    .ToList();

                compileLibraryAssemblyNames.Should().BeEquivalentTo(
                    Net461CompileAssemblies.Concat(new[] { testReference.Name + ".dll" }));
            }
        }

        private static readonly string[] Net461ReferenceOnlyAssemblies = new []
        {
            "Microsoft.Win32.Primitives.dll",
            "netfx.force.conflicts.dll",
            "netstandard.dll",
            "System.AppContext.dll",
            "System.Collections.Concurrent.dll",
            "System.Collections.dll",
            "System.Collections.NonGeneric.dll",
            "System.Collections.Specialized.dll",
            "System.ComponentModel.dll",
            "System.ComponentModel.EventBasedAsync.dll",
            "System.ComponentModel.Primitives.dll",
            "System.ComponentModel.TypeConverter.dll",
            "System.Console.dll",
            "System.Data.Common.dll",
            "System.Diagnostics.Contracts.dll",
            "System.Diagnostics.Debug.dll",
            "System.Diagnostics.FileVersionInfo.dll",
            "System.Diagnostics.Process.dll",
            "System.Diagnostics.StackTrace.dll",
            "System.Diagnostics.TextWriterTraceListener.dll",
            "System.Diagnostics.Tools.dll",
            "System.Diagnostics.TraceSource.dll",
            "System.Diagnostics.Tracing.dll",
            "System.Drawing.Primitives.dll",
            "System.Dynamic.Runtime.dll",
            "System.Globalization.Calendars.dll",
            "System.Globalization.dll",
            "System.Globalization.Extensions.dll",
            "System.IO.Compression.dll",
            "System.IO.Compression.ZipFile.dll",
            "System.IO.dll",
            "System.IO.FileSystem.dll",
            "System.IO.FileSystem.DriveInfo.dll",
            "System.IO.FileSystem.Primitives.dll",
            "System.IO.FileSystem.Watcher.dll",
            "System.IO.IsolatedStorage.dll",
            "System.IO.MemoryMappedFiles.dll",
            "System.IO.Pipes.dll",
            "System.IO.UnmanagedMemoryStream.dll",
            "System.Linq.dll",
            "System.Linq.Expressions.dll",
            "System.Linq.Parallel.dll",
            "System.Linq.Queryable.dll",
            "System.Net.Http.dll",
            "System.Net.NameResolution.dll",
            "System.Net.NetworkInformation.dll",
            "System.Net.Ping.dll",
            "System.Net.Primitives.dll",
            "System.Net.Requests.dll",
            "System.Net.Security.dll",
            "System.Net.Sockets.dll",
            "System.Net.WebHeaderCollection.dll",
            "System.Net.WebSockets.Client.dll",
            "System.Net.WebSockets.dll",
            "System.ObjectModel.dll",
            "System.Reflection.dll",
            "System.Reflection.Extensions.dll",
            "System.Reflection.Primitives.dll",
            "System.Resources.Reader.dll",
            "System.Resources.ResourceManager.dll",
            "System.Resources.Writer.dll",
            "System.Runtime.CompilerServices.VisualC.dll",
            "System.Runtime.dll",
            "System.Runtime.Extensions.dll",
            "System.Runtime.Handles.dll",
            "System.Runtime.InteropServices.dll",
            "System.Runtime.InteropServices.RuntimeInformation.dll",
            "System.Runtime.Numerics.dll",
            "System.Runtime.Serialization.Formatters.dll",
            "System.Runtime.Serialization.Json.dll",
            "System.Runtime.Serialization.Primitives.dll",
            "System.Runtime.Serialization.Xml.dll",
            "System.Security.Claims.dll",
            "System.Security.Cryptography.Algorithms.dll",
            "System.Security.Cryptography.Csp.dll",
            "System.Security.Cryptography.Encoding.dll",
            "System.Security.Cryptography.Primitives.dll",
            "System.Security.Cryptography.X509Certificates.dll",
            "System.Security.Principal.dll",
            "System.Security.SecureString.dll",
            "System.Text.Encoding.dll",
            "System.Text.Encoding.Extensions.dll",
            "System.Text.RegularExpressions.dll",
            "System.Threading.dll",
            "System.Threading.Overlapped.dll",
            "System.Threading.Tasks.dll",
            "System.Threading.Tasks.Parallel.dll",
            "System.Threading.Thread.dll",
            "System.Threading.ThreadPool.dll",
            "System.Threading.Timer.dll",
            "System.ValueTuple.dll",
            "System.Xml.ReaderWriter.dll",
            "System.Xml.XDocument.dll",
            "System.Xml.XmlDocument.dll",
            "System.Xml.XmlSerializer.dll",
            "System.Xml.XPath.dll",
            "System.Xml.XPath.XDocument.dll",
        };

        private static readonly string[] Net461CompileAssemblies = Net461ReferenceOnlyAssemblies.Concat(new[]
        {
            "CopyLocalFalseReferences.exe",
            "mscorlib.dll",
            "System.ComponentModel.Annotations.dll",
            "System.Core.dll",
            "System.Data.dll",
            "System.dll",
            "System.Drawing.dll",
            "System.IO.Compression.FileSystem.dll",
            "System.Numerics.dll",
            "System.Reflection.Emit.dll",
            "System.Reflection.Emit.ILGeneration.dll",
            "System.Reflection.Emit.Lightweight.dll",
            "System.Runtime.InteropServices.WindowsRuntime.dll",
            "System.Runtime.Serialization.dll",
            "System.ServiceModel.Duplex.dll",
            "System.ServiceModel.Http.dll",
            "System.ServiceModel.NetTcp.dll",
            "System.ServiceModel.Primitives.dll",
            "System.ServiceModel.Security.dll",
            "System.Xml.dll",
            "System.Xml.Linq.dll",
        }).ToArray();
    }
}