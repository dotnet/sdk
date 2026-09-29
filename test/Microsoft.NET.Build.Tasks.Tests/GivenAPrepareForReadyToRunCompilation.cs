// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
                .Which.GetMetadata(MetadataKeys.OutputR2RImage).Should().Be(Path.Combine(outputPath, "sub/Library.wasm"));

            ITaskItem fileToPublish = task.ReadyToRunFilesToPublish.Should().ContainSingle().Which;
            fileToPublish.ItemSpec.Should().Be(Path.Combine(outputPath, "sub/Library.wasm"));
            fileToPublish.GetMetadata(MetadataKeys.RelativePath).Should().Be("sub/Library.wasm");
            fileToPublish.GetMetadata(MetadataKeys.RequiresNativeLink).Should().BeEmpty();
        }

        [TestMethod]
        public void It_preserves_macho_native_link_metadata_for_separately_compiled_images()
        {
            string outputPath = Path.Combine("obj", "r2r");
            TaskItem assembly = CreateAssemblyItem("sub/Library.dll");
            PrepareForReadyToRunCompilation task = CreateTask(outputPath, "macho", composite: false, assembly);

            task.Execute().Should().BeTrue();

            string compilerOutput = Path.Combine(outputPath, "sub/Library.o");
            task.ReadyToRunCompileList.Should().ContainSingle()
                .Which.GetMetadata(MetadataKeys.OutputR2RImage).Should().Be(compilerOutput);

            ITaskItem fileToPublish = task.ReadyToRunFilesToPublish.Should().ContainSingle().Which;
            fileToPublish.ItemSpec.Should().Be(Path.Combine(outputPath, "sub/Library.dylib"));
            fileToPublish.GetMetadata(MetadataKeys.RelativePath).Should().Be("sub/Library.dylib");
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
                .Single(item => item.GetMetadata(MetadataKeys.RelativePath) == "sub/Component.dll");
            componentFileToPublish.ItemSpec.Should().Be(Path.Combine(outputPath, "Component.dll"));
            componentFileToPublish.GetMetadata(MetadataKeys.RequiresNativeLink).Should().BeEmpty();
            componentFileToPublish.GetMetadata(MetadataKeys.NativeLinkerInputPath).Should().BeEmpty();

            task.ReadyToRunCompositeBuildInput.Should().ContainSingle()
                .Which.GetMetadata(MetadataKeys.RelativePath).Should().Be("sub/Component.dll");
        }

        [TestMethod]
        public void It_uses_wasm_paths_for_the_composite_owner_and_component_stubs()
        {
            string outputPath = Path.Combine("obj", "r2r");
            TaskItem component = CreateAssemblyItem("sub/Component.dll");
            PrepareForReadyToRunCompilation task = CreateTask(outputPath, "wasm", composite: true, component);
            string ownerRelativePath = Path.ChangeExtension(Path.GetFileName(task.MainAssembly.ItemSpec), ".r2r.wasm");

            task.Execute().Should().BeTrue();

            task.ReadyToRunCompileList.Should().ContainSingle()
                .Which.GetMetadata(MetadataKeys.OutputR2RImage).Should().Be(Path.Combine(outputPath, ownerRelativePath));

            ITaskItem owner = task.ReadyToRunFilesToPublish
                .Single(item => item.GetMetadata(MetadataKeys.RelativePath) == ownerRelativePath);
            owner.ItemSpec.Should().Be(Path.Combine(outputPath, ownerRelativePath));
            owner.GetMetadata(MetadataKeys.RequiresNativeLink).Should().BeEmpty();
            owner.GetMetadata(MetadataKeys.NativeLinkerInputPath).Should().BeEmpty();

            ITaskItem componentFileToPublish = task.ReadyToRunFilesToPublish
                .Single(item => item.GetMetadata(MetadataKeys.RelativePath) == "sub/Component.wasm");
            componentFileToPublish.ItemSpec.Should().Be(Path.Combine(outputPath, "Component.wasm"));
            componentFileToPublish.GetMetadata(MetadataKeys.RequiresNativeLink).Should().BeEmpty();
            componentFileToPublish.GetMetadata(MetadataKeys.NativeLinkerInputPath).Should().BeEmpty();
            task.ReadyToRunCompositeBuildInput.Should().ContainSingle()
                .Which.GetMetadata(MetadataKeys.RelativePath).Should().Be("sub/Component.dll");
        }

        [TestMethod]
        public void It_uses_the_fixed_wasm_path_for_a_wasi_composite_owner()
        {
            string outputPath = Path.Combine("obj", "r2r");
            TaskItem component = CreateAssemblyItem("sub/Component.dll");
            PrepareForReadyToRunCompilation task = CreateTask(outputPath, "wasm", composite: true, component);
            task.Crossgen2Tool.SetMetadata(MetadataKeys.TargetOS, "wasi");

            task.Execute().Should().BeTrue();

            task.ReadyToRunCompileList.Should().ContainSingle()
                .Which.GetMetadata(MetadataKeys.OutputR2RImage).Should().Be(Path.Combine(outputPath, "composite-r2r.wasm"));

            ITaskItem owner = task.ReadyToRunFilesToPublish
                .Single(item => item.GetMetadata(MetadataKeys.RelativePath) == "composite-r2r.wasm");
            owner.ItemSpec.Should().Be(Path.Combine(outputPath, "composite-r2r.wasm"));

            ITaskItem componentFileToPublish = task.ReadyToRunFilesToPublish
                .Single(item => item.GetMetadata(MetadataKeys.RelativePath) == "sub/Component.wasm");
            componentFileToPublish.ItemSpec.Should().Be(Path.Combine(outputPath, "Component.wasm"));
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
