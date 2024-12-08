// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Win32;
using Microsoft.Win32.Msi;

if (args.Length < 3)
{
    Console.WriteLine("Invalid arguments. Usage: finalizer <logPath> <sdkVersion> <platform>");
    return;
}

string logPath = args[0];
string sdkVersion = args[1];
string platform = args[2];

using StreamWriter logStream = new StreamWriter(logPath);

Logger.Init(logStream);

Logger.Log($"{nameof(logPath)}: {logPath}");
Logger.Log($"{nameof(sdkVersion)}: {sdkVersion}");
Logger.Log($"{nameof(platform)}: {platform}");

try
{
    // Step 1: Parse and format SDK feature band version
    string featureBandVersion = new SdkFeatureBand(sdkVersion).ToString();

    // Step 2: Check if SDK feature band is installed
    bool isInstalled = DetectSdk(featureBandVersion, platform);
    if (isInstalled)
    {
        Logger.Log($"SDK with feature band {featureBandVersion} is already installed.");
        return;
    }

    // Step 3: Remove dependent components if necessary
    bool restartRequired = RemoveDependent(featureBandVersion);
    if (restartRequired)
    {
        Logger.Log("A restart may be required after removing the dependent component.");
    }

    // Step 4: Delete workload records
    DeleteWorkloadRecords(featureBandVersion, platform);

    // Step 5: Clean up install state file
    RemoveInstallStateFile(featureBandVersion, platform);

    // Final reboot check
    if (restartRequired || IsRebootPending())
    {
        Logger.Log("A system restart is recommended to complete the operation.");
    }
    else
    {
        Logger.Log("Operation completed successfully. No restart is required.");
    }
}
catch (Exception ex)
{
    Logger.Log($"Error: {ex}");
}

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
    // Disable MSI UI
    _ = MsiSetInternalUI((uint)InstallUILevel.NoChange, IntPtr.Zero);

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
        if (hkProviderKey is null) continue;

        // Open the Dependents subkey
        using var hkDependentsKey = hkProviderKey.OpenSubKey("Dependents", writable: true);
        if (hkDependentsKey is null) continue;

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
                string? productCode = hkProviderKey.GetValue("ProductId")?.ToString();
                if (productCode is null)
                {
                    Logger.Log($"Can't find ProductId in {providerKeyName}");
                    return false;
                }

                // Configure the product to be absent (uninstall the product)
                uint error = MsiConfigureProductEx(productCode, (int)InstallUILevel.Default, InstallState.ABSENT, "");
                Logger.Log("Product configured to absent successfully.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Error handling product configuration: {ex.Message}");
                return false;
            }
        }
        return true;
    }

    return false;
}

static void DeleteWorkloadRecords(string featureBandVersion, string platform)
{
    string workloadKey = $@"SOFTWARE\Microsoft\dotnet\InstalledWorkloads\Standalone\{platform}";

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

static bool IsRebootPending()
{
    using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
    {
        if (key is not null)
        {
            Logger.Log("Reboot is pending due to component-based servicing.");
            return true;
        }
    }

    using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager"))
    {
        var value = key?.GetValue("PendingFileRenameOperations");
        if (value is not null)
        {
            Logger.Log("Pending file rename operations indicate a reboot is pending.");
            return true;
        }
    }

    Logger.Log("No reboot pending.");
    return false;
}

[DllImport("msi.dll", CharSet = CharSet.Unicode)]
[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
static extern uint MsiConfigureProductEx(string szProduct, int iInstallLevel, InstallState eInstallState, string szCommandLine);

[DllImport("msi.dll", CharSet = CharSet.Unicode)]
[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
static extern uint MsiSetInternalUI(uint dwUILevel, IntPtr phWnd);

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
