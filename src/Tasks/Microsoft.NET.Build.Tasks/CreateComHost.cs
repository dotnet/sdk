using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks
{
    public class CreateComHost : TaskBase
    {
        [Required]
        public string ComHostSourcePath { get; set; }

        [Required]
        public string ComHostDestinationPath { get; set; }

        [Required]
        public string ClsidMapPath { get; set; }

        protected override void ExecuteCore()
        {
            ComHost.Create(
                ComHostSourcePath,
                ComHostDestinationPath,
                ClsidMapPath);
        }
    }
}
