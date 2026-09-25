// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [TestClass]
    public class GivenAPrepareForReadyToRunCompilation
    {
        [TestMethod]
        public void It_uses_wasm_paths_for_separately_compiled_images()
        {
            string outputPath = Path.Combine("obj", "r2r");
            TaskItem assembly = CreateAssemblyItem("sub/Library.dll");
            PrepareForReadyToRunCompilation task = CreateTask(outputPath, "wasm", composite: false, assembly);

            task.Execute().Should().BeTrue();

            task.ReadyToRunCompileList.Should().ContainSingle()
                .Which.GetMetadata(MetadataKeys.OutputR2RImage).Should().Be(Path.Combine(outputPath, "sub", "Library.wasm"));

            ITaskItem fileToPublish = task.ReadyToRunFilesToPublish.Should().ContainSingle().Which;
            fileToPublish.ItemSpec.Should().Be(Path.Combine(outputPath, "sub", "Library.wasm"));
            fileToPublish.GetMetadata(MetadataKeys.RelativePath).Should().Be(Path.Combine("sub", "Library.wasm"));
            fileToPublish.GetMetadata(MetadataKeys.RequiresNativeLink).Should().BeEmpty();
        }

        [TestMethod]
        public void It_preserves_macho_native_link_metadata_for_separately_compiled_images()
        {
            string outputPath = Path.Combine("obj", "r2r");
            TaskItem assembly = CreateAssemblyItem("sub/Library.dll");
            PrepareForReadyToRunCompilation task = CreateTask(outputPath, "macho", composite: false, assembly);

            task.Execute().Should().BeTrue();

            string compilerOutput = Path.Combine(outputPath, "sub", "Library.o");
            task.ReadyToRunCompileList.Should().ContainSingle()
                .Which.GetMetadata(MetadataKeys.OutputR2RImage).Should().Be(compilerOutput);

            ITaskItem fileToPublish = task.ReadyToRunFilesToPublish.Should().ContainSingle().Which;
            fileToPublish.ItemSpec.Should().Be(Path.Combine(outputPath, "sub", "Library.dylib"));
            fileToPublish.GetMetadata(MetadataKeys.RelativePath).Should().Be(Path.Combine("sub", "Library.dylib"));
            fileToPublish.GetMetadata(MetadataKeys.RequiresNativeLink).Should().Be("true");
            fileToPublish.GetMetadata(MetadataKeys.NativeLinkerInputPath).Should().Be(compilerOutput);
        }

        [TestMethod]
        public void It_does_not_apply_macho_link_metadata_to_composite_component_images()
        {
            string outputPath = Path.Combine("obj", "r2r");
            TaskItem component = CreateAssemblyItem("sub/Component.dll");
            PrepareForReadyToRunCompilation task = CreateTask(outputPath, "macho", composite: true, component);

            task.Execute().Should().BeTrue();

            ITaskItem componentFileToPublish = task.ReadyToRunFilesToPublish
                .Single(item => item.GetMetadata(MetadataKeys.RelativePath) == Path.Combine("sub", "Component.dll"));
            componentFileToPublish.ItemSpec.Should().Be(Path.Combine(outputPath, "Component.dll"));
            componentFileToPublish.GetMetadata(MetadataKeys.RequiresNativeLink).Should().BeEmpty();
            componentFileToPublish.GetMetadata(MetadataKeys.NativeLinkerInputPath).Should().BeEmpty();

            task.ReadyToRunCompositeBuildInput.Should().ContainSingle()
                .Which.GetMetadata(MetadataKeys.RelativePath).Should().Be(Path.Combine("sub", "Component.dll"));
        }

        [TestMethod]
        public void Crossgen_targets_track_private_compiler_inputs_and_arguments()
        {
            string targetsPath = Path.Combine(AppContext.BaseDirectory, "SdkTargets", "Microsoft.NET.CrossGen.targets");
            XDocument targets = XDocument.Load(targetsPath);
            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            XElement createImagesTarget = targets.Root!
                .Elements(ns + "Target")
                .Single(target => target.Attribute("Name")!.Value == "_CreateR2RImages");

            string inputs = createImagesTarget.Attribute("Inputs")!.Value;
            inputs.Should().Contain("@(CrossgenTool)");
            inputs.Should().Contain("@(Crossgen2Tool)");
            inputs.Should().Contain("@(_ReadyToRunCompilerInputs)");

            XElement createSymbolsTarget = targets.Root!
                .Elements(ns + "Target")
                .Single(target => target.Attribute("Name")!.Value == "_CreateR2RSymbols");

            createImagesTarget.Elements(ns + "RunReadyToRunCompiler").Single()
                .Attribute("Crossgen2ExtraCommandLineArgs")!.Value.Should().Contain("$(_PublishReadyToRunCrossgen2ExtraArgs)");
            createSymbolsTarget.Elements(ns + "RunReadyToRunCompiler").Single()
                .Attribute("Crossgen2ExtraCommandLineArgs")!.Value.Should().Contain("$(_PublishReadyToRunCrossgen2ExtraArgs)");
        }

        private static PrepareForReadyToRunCompilation CreateTask(string outputPath, string containerFormat, bool composite, params ITaskItem[] assemblies)
        {
            TaskItem crossgen2Tool = new("crossgen2");
            crossgen2Tool.SetMetadata(MetadataKeys.IsVersion5, "false");
            crossgen2Tool.SetMetadata(MetadataKeys.TargetOS, "osx");

            return new PrepareForReadyToRunCompilation
            {
                BuildEngine = new MockBuildEngine(),
                MainAssembly = CreateAssemblyItem("App.dll"),
                Assemblies = assemblies,
                OutputPath = outputPath,
                IncludeSymbolsInSingleFile = false,
                ReadyToRunUseCrossgen2 = true,
                Crossgen2Tool = crossgen2Tool,
                Crossgen2ContainerFormat = containerFormat,
                Crossgen2Composite = composite,
            };
        }

        private static TaskItem CreateAssemblyItem(string relativePath)
        {
            TaskItem item = new(typeof(GivenAPrepareForReadyToRunCompilation).Assembly.Location);
            item.SetMetadata(MetadataKeys.RelativePath, relativePath);
            return item;
        }
    }
}
