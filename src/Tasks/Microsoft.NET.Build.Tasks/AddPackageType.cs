using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;
using System;
using System.Globalization;

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
            // Normalize input
            string current = CurrentPackageType ?? string.Empty;
            string toAdd = (PackageTypeToAdd ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(toAdd))
            {
                UpdatedPackageType = current;
                return true;
            }

            // Pad with semicolons for easier matching
            string padded = ";" + current.Replace(" ", string.Empty).Trim().ToLowerInvariant() + ";";
            string toAddLower = toAdd.ToLowerInvariant();

            // Check for exact match or versioned match
            if (padded.Contains($";{toAddLower};") || padded.Contains($";{toAddLower},"))
            {
                UpdatedPackageType = current;
            }
            else if (string.IsNullOrEmpty(current))
            {
                UpdatedPackageType = toAdd;
            }
            else
            {
                UpdatedPackageType = toAdd + ";" + current;
            }
            return true;
        }
    }
}
