using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.NET.Build.Tasks
{
    public class AddPackageType : Task
    {
        public string? CurrentPackageType { get; set; }

        [Required]
        public string? PackageTypeToAdd { get; set; }

        [Output]
        public string[]? UpdatedPackageType { get; set; }

        public override bool Execute()
        {
            string current = CurrentPackageType ?? string.Empty;
            string toAdd = (PackageTypeToAdd ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(toAdd))
            {
                UpdatedPackageType = current.Split(';');
                return true;
            }

            // Split current types, trim, and filter out empty
            var types = current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            // Check if toAdd (case-insensitive, ignoring version) is already present
            bool alreadyPresent = types.Any(t =>
            {
                var typeName = t.Split(',')[0].Trim();
                return typeName.Equals(toAdd, StringComparison.InvariantCultureIgnoreCase);
            });


            if (alreadyPresent)
            {
                UpdatedPackageType = current.Split(';');
            }
            else
            {
                types.Insert(0, toAdd);
                UpdatedPackageType = types.ToArray();
            }
            return true;
        }
    }
}
