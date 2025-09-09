// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using Microsoft.DotNet.Cli.Commands.Run;
using Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;

namespace Microsoft.DotNet.Cli.Commands.Test;

/// <summary>
/// Groups test modules that should NOT be run in parallel.
/// The whole group is still parallelizable with other groups, just that the inner modules are run sequentially.
/// This class serves only the purpose of disabling parallelizing of TFMs when user sets TestTfmsInParallel to false.
/// For a single TFM project, we will use the constructor with a single module. Meaning it will be parallelized.
/// For a multi TFM project:
/// - If parallelization is enabled, we will create multiple ParallelizableTestModuleGroupWithSequentialInnerModuless, each with a single module.
/// - If parallelization is not enabled, we will create a single ParallelizableTestModuleGroupWithSequentialInnerModules with all modules.
/// </summary>
internal sealed class ParallelizableTestModuleGroupWithSequentialInnerModules : IEnumerable<TestModule>
{
    public ParallelizableTestModuleGroupWithSequentialInnerModules(List<TestModule> modules)
    {
        Modules = modules;
    }

    public ParallelizableTestModuleGroupWithSequentialInnerModules(TestModule module)
    {
        // This constructor is used when there is only one module.
        Module = module;
    }

    public List<TestModule>? Modules { get; }

    public TestModule? Module { get; }

    public TestModule[] GetVSTestAndNotMTPModules()
    {
        if (Modules is not null)
        {
            return Modules.Where(module => !module.IsTestingPlatformApplication).ToArray();
        }

        Debug.Assert(Module is not null);
        if (!Module.IsTestingPlatformApplication)
        {
            return [Module];
        }

        return Array.Empty<TestModule>();
    }

    public Enumerator GetEnumerator()
        => new Enumerator(this);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    IEnumerator<TestModule> IEnumerable<TestModule>.GetEnumerator() => GetEnumerator();

    internal struct Enumerator : IEnumerator<TestModule>
    {
        private readonly ParallelizableTestModuleGroupWithSequentialInnerModules _group;
        private int _index = -1;

        public Enumerator(ParallelizableTestModuleGroupWithSequentialInnerModules group)
        {
            _group = group;
        }

        public TestModule Current
        {
            get
            {
                if (_index < 0)
                {
                    throw new InvalidOperationException();
                }

                if (_group.Modules is not null)
                {
                    return _group.Modules[_index];
                }

                if (_index != 0)
                {
                    throw new InvalidOperationException();
                }

                Debug.Assert(_group.Module is not null);
                return _group.Module;
            }
        }

        object IEnumerator.Current => Current;

        public void Dispose() { }

        public bool MoveNext()
        {
            _index++;

            if (_group.Modules is not null)
            {
                return _index < _group.Modules.Count;
            }

            return _index == 0;
        }

        public void Reset() => _index = -1;
    }
}


internal sealed record TestModule(RunProperties RunProperties, string? ProjectFullPath, string? TargetFramework, bool IsTestingPlatformApplication, bool IsTestProject, ProjectLaunchSettingsModel? LaunchSettings, string TargetPath, string? DotnetRootArchVariableName);

internal sealed record CommandLineOption(string Name, string Description, bool? IsHidden, bool? IsBuiltIn);
