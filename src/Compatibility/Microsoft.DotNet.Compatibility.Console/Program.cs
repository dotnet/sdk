// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Compatibility.Console;
using Microsoft.DotNet.PackageValidation;

if (args.Length != 11)
{
    throw new ArgumentException("Pass in the set of required parameters...");
}

string packageTargetPath = args[0];
string compatibilitySuppressionFilePath = args[1];
bool generateCompatibilitySuppressionFile = bool.Parse(args[2]);
bool runApiCompat = bool.Parse(args[3]);
bool enableStrictModeForCompatibleTfms = bool.Parse(args[4]);
bool enableStrictModeForCompatibleFrameworksInPackage = bool.Parse(args[5]);
bool disablePackageBaselineValidation = bool.Parse(args[6]);
string baselinePackageTargetPath = args[7];
string noWarn = args[8];
string runtimeGraph = args[9];
string[] referencePathItems = args[10].Split(',');

Dictionary<string, HashSet<string>> apiCompatReferences = new();
if (referencePathItems != null)
{
    foreach (string referencePathItem in referencePathItems)
    {
        string[] referencePathItemParts = referencePathItem.Split('=');
        string tfm = referencePathItemParts[0];
        if (string.IsNullOrEmpty(tfm))
            continue;

        string referencePath = referencePathItemParts[1];
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

Package package = Package.Create(packageTargetPath, runtimeGraph);
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
