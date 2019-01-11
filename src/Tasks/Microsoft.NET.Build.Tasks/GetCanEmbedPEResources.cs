using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class GetCanEmbedPEResources : TaskBase
    {
        [Output]
        public bool CanEmbedResources { get; set; }

        protected override void ExecuteCore()
        {
            CanEmbedResources = ResourceUpdater.IsSupportedOS();
        }
    }
}
