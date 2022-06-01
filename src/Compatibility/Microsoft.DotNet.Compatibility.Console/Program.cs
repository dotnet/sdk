// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.PackageValidation;

namespace Microsoft.DotNet.Compatibility.Console
{
    class Program
    {
        /// <summary>
        /// Microsoft.DotNet.Compatibility.Console
        /// </summary>
        /// <param name="argument">The path to the package to be checked for compatibility</param>
        /// <param name="compatibilitySuppressionFilePath">The path to the compatibility suppression file</param>
        /// <param name="generateCompatibilitySuppressionFile">If true, generates a compatibility suppression file</param>
        /// <param name="runApiCompat">If true, runs ApiCompat as part of this tool</param>
        /// <param name="enableStrictModeForCompatibleTfms">If true, enables strict mode for the compatible tfm validator</param>
        /// <param name="enableStrictModeForCompatibleFrameworksInPackage">If true, enables strict mode for the compatible frameworks validator</param>
        /// <param name="disablePackageBaselineValidation">If true, disables the package baseline validator</param>
        /// <param name="baselinePackageTargetPath">The path to the baseline package to compare against</param>
        /// <param name="noWarn">A NoWarn string that allows to disable specific rules</param>
        /// <param name="runtimeGraph">The path to the runtime graph file</param>
        /// <param name="referencePathItems">A string containing tuples of reference assembly paths plus the target framework.</param>
        static void Main(string argument,
            string compatibilitySuppressionFilePath = "",
            bool generateCompatibilitySuppressionFile = false,
            bool runApiCompat = true,
            bool enableStrictModeForCompatibleTfms = true,
            bool enableStrictModeForCompatibleFrameworksInPackage = true,
            bool disablePackageBaselineValidation = false,
            string baselinePackageTargetPath = "",
            string noWarn = "",
            string runtimeGraph = "",
            List<(string tfm, string referencePath)>? referencePathItems = null)
        {
            Dictionary<string, HashSet<string>> apiCompatReferences = new();
            if (referencePathItems != null)
            {
                foreach (var (tfm, referencePath) in referencePathItems)
                {
                    if (string.IsNullOrEmpty(tfm))
                        continue;

                    if (!File.Exists(referencePath))
                        continue;

                    if (!apiCompatReferences.TryGetValue(tfm, out HashSet<string>? directories))
                    {
                        directories = new();
                        apiCompatReferences.Add(tfm, directories);
                    }

                    directories.Add(referencePath);
                }
            }

            Package package = Package.Create(argument, runtimeGraph);
            ConsoleCompatibilityLogger logger = new(compatibilitySuppressionFilePath, generateCompatibilitySuppressionFile, noWarn);

            new CompatibleTfmValidator(runApiCompat, enableStrictModeForCompatibleTfms, logger, apiCompatReferences).Validate(package);
            new CompatibleFrameworkInPackageValidator(enableStrictModeForCompatibleFrameworksInPackage, logger, apiCompatReferences).Validate(package);

            if (!disablePackageBaselineValidation && !string.IsNullOrEmpty(baselinePackageTargetPath))
            {
                Package baselinePackage = Package.Create(baselinePackageTargetPath, runtimeGraph);
                new BaselinePackageValidator(baselinePackage, runApiCompat, logger, apiCompatReferences).Validate(package);
            }

            if (generateCompatibilitySuppressionFile)
            {
                logger.WriteSuppressionFile();
            }
        }
    }
}
