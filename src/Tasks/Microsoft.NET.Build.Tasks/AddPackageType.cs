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
        [Required]
        public string? CurrentPackageType { get; set; }

        [Required]
        public string? PackageTypeToAdd { get; set; }

        [Output]
        public string? UpdatedPackageType { get; set; }

        public override bool Execute()
        {
            string current = CurrentPackageType ?? string.Empty;
            string toAdd = (PackageTypeToAdd ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(toAdd))
            {
                UpdatedPackageType = current;
                return true;
            }

            // Split current types, trim, and filter out empty
            var types = current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            // Check if toAdd (case-insensitive, ignoring version) is already present
            string toAddLower = toAdd.ToLowerInvariant();
            bool alreadyPresent = types.Any(t =>
            {
                var typeName = t.Split(',')[0].Trim().ToLowerInvariant();
                return typeName == toAddLower;
            });

            if (!alreadyPresent)
            {
                types.Insert(0, toAdd);
            }

            UpdatedPackageType = string.Join(";", types);
            return true;
        }
    }
}
