using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyModel;

namespace Microsoft.NET.Build.Tasks
{
    internal class DependencyContextBuilder2
    {
        private readonly SingleProjectInfo _mainProjectInfo;
        private bool _includeMainProjectInDepsFile = true;

        public DependencyContextBuilder2(SingleProjectInfo mainProjectInfo)
        {
            _mainProjectInfo = mainProjectInfo;
        }

        public DependencyContextBuilder2 WithMainProjectInDepsFile(bool includeMainProjectInDepsFile)
        {
            _includeMainProjectInDepsFile = includeMainProjectInDepsFile;
            return this;
        }

        public DependencyContext Build()
        {

            IEnumerable<RuntimeLibrary> runtimeLibraries = Enumerable.Empty<RuntimeLibrary>();

            if (_includeMainProjectInDepsFile)
            {
                runtimeLibraries = runtimeLibraries.Concat(new[]
                {
                    GetProjectRuntimeLibrary()
                });
            }

            throw new NotImplementedException();
        }

        private RuntimeLibrary GetProjectRuntimeLibrary()
        {
            RuntimeAssetGroup[] runtimeAssemblyGroups = new[] { new RuntimeAssetGroup(string.Empty, _mainProjectInfo.OutputName) };

        }
    }
}
