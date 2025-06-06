// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Linq;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Removes a target from a semicolon-delimited list of targets.
    /// </summary>
    public class RemoveTargetFromList : TaskBase
    {
        [Required]
        public string TargetList { get; set; }

        [Required]
        public string TargetToRemove { get; set; }

        //  Output needs to be an array so MSBuild won't escape semicolonns
        [Output]
        public string[] UpdatedTargetList { get; private set; }

        protected override void ExecuteCore()
        {
            UpdatedTargetList = (TargetList ?? string.Empty)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => !string.Equals(t.Trim(), TargetToRemove, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }
}
