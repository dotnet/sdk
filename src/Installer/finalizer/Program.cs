// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Win32;
using Microsoft.Win32.Msi;

if (args.Length < 3)
{
    return (int)Error.INVALID_COMMAND_LINE;
}

string logPath = args[0];
string sdkVersion = args[1];
string platform = args[2];

using StreamWriter logStream = new StreamWriter(logPath);

Logger.Init(logStream);

Logger.Log($"{nameof(logPath)}: {logPath}");
Logger.Log($"{nameof(sdkVersion)}: {sdkVersion}");
Logger.Log($"{nameof(platform)}: {platform}");
int exitCode = (int)Error.SUCCESS;

try
{
    // Step 1: Parse and format SDK feature band version
    string featureBandVersion = new SdkFeatureBand(sdkVersion).ToString();
    string dependent = $"Microsoft.NET.Sdk,{featureBandVersion},{platform}";

    // Step 2: Check if SDK feature band is installed
    bool isInstalled = DetectSdk(featureBandVersion, platform);
    if (isInstalled)
    {
        Logger.Log($"SDK with feature band {featureBandVersion} is already installed.");
        return (int)Error.SUCCESS;
    }

    // Step 3: Remove dependent components if necessary
    if (RemoveDependent(dependent))
    {
        // Pass potential restart exit codes back to the bundle based on executing the
        // workload related MSIs. The bundle may take additional actions such as prompting the user.
        exitCode = (int)Error.SUCCESS_REBOOT_REQUIRED;
    };

    // Step 4: Delete workload records
    DeleteWorkloadRecords(featureBandVersion, platform);

    // Step 5: Clean up install state file
    RemoveInstallStateFile(featureBandVersion, platform);
}
catch (Exception ex)
{
    Logger.Log($"Error: {ex}");
    exitCode = ex.HResult;
}

return exitCode;

static bool DetectSdk(string featureBandVersion, string platform)
{
    string registryPath = $@"SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\{platform}\sdk";
    using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(registryPath))
    {
        if (key is null)
        {
            Logger.Log("SDK registry path not found.");
            return false;
        }

        foreach (var valueName in key.GetValueNames())
        {
            if (valueName.Contains(featureBandVersion))
            {
                Logger.Log($"SDK version detected: {valueName}");
                return true;
            }
        }
    }
    return false;
}

static bool RemoveDependent(string dependent)
{
    bool restartRequired = false;

    // Open the installer dependencies registry key
    // This has to be an exhaustive search as we're not looking for a specific provider key, but for a specific dependent
    // that could be registered against any provider key.
    using var hkInstallerDependenciesKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\Installer\Dependencies", writable: true);
    if (hkInstallerDependenciesKey is null)
    {
        Logger.Log("Installer dependencies key does not exist.");
        return false;
    }

    // Iterate over each provider key in the dependencies
    foreach (string providerKeyName in hkInstallerDependenciesKey.GetSubKeyNames())
    {
        Logger.Log($"Processing provider key: {providerKeyName}");

        using var hkProviderKey = hkInstallerDependenciesKey.OpenSubKey(providerKeyName, writable: true);
        if (hkProviderKey is null)
        {
            continue;
        }

        // Open the Dependents subkey
        using var hkDependentsKey = hkProviderKey.OpenSubKey("Dependents", writable: true);
        if (hkDependentsKey is null)
        {
            continue;
        }

        // Check if the dependent exists and continue if it does not
        bool dependentExists = false;
        foreach (string dependentsKeyName in hkDependentsKey.GetSubKeyNames())
        {
            if (string.Equals(dependentsKeyName, dependent, StringComparison.OrdinalIgnoreCase))
            {
                dependentExists = true;
                break;
            }
        }

        if (!dependentExists)
        {
            continue;
        }

        Logger.Log($"Dependent match found: {dependent}");

        // Attempt to remove the dependent key
        try
        {
            hkDependentsKey.DeleteSubKey(dependent);
            Logger.Log("Dependent deleted");
        }
        catch (Exception ex)
        {
            Logger.Log($"Exception while removing dependent key: {ex.Message}");
            return false;
        }

        // Check if any dependents are left
        if (hkDependentsKey.SubKeyCount == 0)
        {
            // No remaining dependents, handle product uninstallation
            try
            {
                // Default value should be a REG_SZ containing the product code.
                string? productCode = hkProviderKey.GetValue(null) as string;

                if (productCode is null)
                {
                    Logger.Log($"No product ID found, provider key: {providerKeyName}");
                    continue;
                }

                // Let's make sure the product is actually installed. The provider key for an MSI typically
                // stores the ProductCode, DisplayName, and Version, but by calling into MsiGetProductInfo,
                // we're doing an implicit detect and getting a property back. This avoids reading additional
                // registry keys.
                uint error = WindowsInstaller.GetProductInfo(productCode, "ProductName", out string productName);

                if (error != Error.SUCCESS)
                {
                    Logger.Log($"Failed to detect product, ProductCode: {productCode}, result: 0x{error:x8}");
                    continue;
                }

                // Need to set the UI level before executing the MSI.
                _ = WindowsInstaller.SetInternalUI(InstallUILevel.None);

                // Configure the product to be absent (uninstall the product)
                error = WindowsInstaller.ConfigureProduct(productCode,
                    WindowsInstaller.INSTALLLEVEL_DEFAULT,
                    InstallState.ABSENT,
                    "MSIFASTINSTALL=7 IGNOREDEPENDENCIES=ALL REBOOT=ReallySuppress");
                Logger.Log($"Uninstall of {productName} ({productCode}) exited with 0x{error:x8}");

                if (error == Error.SUCCESS_REBOOT_INITIATED || error == Error.SUCCESS_REBOOT_REQUIRED)
                {
                    restartRequired = true;
                }

                // Remove the provider key. Typically these are removed by the engine, but since the workload
                // packs and manifest were installed by the CLI, the finalizer needs to clean these up.
                hkInstallerDependenciesKey.DeleteSubKeyTree(providerKeyName, throwOnMissingSubKey: false);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to process dependentprocess: {ex.Message}");
                return restartRequired;
            }
        }
    }

    return restartRequired;
}

static void DeleteWorkloadRecords(string featureBandVersion, string platform)
{
    string? workloadKey = $@"SOFTWARE\Microsoft\dotnet\InstalledWorkloads\Standalone\{platform}";

    using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(workloadKey, writable: true))
    {
        if (key is not null)
        {
            key.DeleteSubKeyTree(featureBandVersion, throwOnMissingSubKey: false);
            Logger.Log($"Deleted workload records for '{featureBandVersion}'.");
        }
        else
        {
            Logger.Log("No workload records found to delete.");
        }
    }

    DeleteEmptyKeyToRoot(Registry.LocalMachine, workloadKey);
}

static void DeleteEmptyKeyToRoot(RegistryKey key, string name)
{
    string? subKeyName = Path.GetFileName(name.TrimEnd(Path.DirectorySeparatorChar));
    string? tempName = name;

    while (!string.IsNullOrWhiteSpace(tempName))
    {
        using (RegistryKey? k = key.OpenSubKey(tempName))
        {
            if (k is not null && k.SubKeyCount == 0 && k.ValueCount == 0)
            {
                tempName = Path.GetDirectoryName(tempName);

                if (tempName is not null)
                {
                    try
                    {
                        using (RegistryKey? parentKey = key.OpenSubKey(tempName, writable: true))
                        {
                            if (parentKey is not null)
                            {
                                parentKey.DeleteSubKeyTree(subKeyName, throwOnMissingSubKey: false);
                                Logger.Log($"Deleted empty key: {subKeyName}");
                                subKeyName = Path.GetFileName(tempName.TrimEnd(Path.DirectorySeparatorChar));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Failed to delete key: {tempName}, error: {ex.Message}");
                        break;
                    }
                }
            }
            else
            {
                break;
            }
        }
    }
}

static void RemoveInstallStateFile(string featureBandVersion, string platform)
{
    string programDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    string installStatePath = Path.Combine(programDataPath, "dotnet", "workloads", platform, featureBandVersion, "installstate", "default.json");

    if (File.Exists(installStatePath))
    {
        File.Delete(installStatePath);
        Logger.Log($"Deleted install state file: {installStatePath}");

        var dir = new DirectoryInfo(installStatePath).Parent;
        while (dir is not null && dir.Exists && dir.GetFiles().Length == 0 && dir.GetDirectories().Length == 0)
        {
            dir.Delete();
            dir = dir.Parent;
        }
    }
    else
    {
        Logger.Log("Install state file does not exist.");
    }
}

static class Logger
{
    static StreamWriter? s_logStream;

    public static void Init(StreamWriter logStream) => s_logStream = logStream;

    public static void Log(string message)
    {
        var pid = Environment.ProcessId;
        var tid = Environment.CurrentManagedThreadId;
        s_logStream!.WriteLine($"[{pid:X4}:{tid:X4}][{DateTime.Now:yyyy-MM-ddTHH:mm:ss}] Finalizer: {message}");
    }
}
