// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Win32;
using WixToolset.Dtf.WindowsInstaller;

if (args.Length < 3)
{
  Console.WriteLine("Invalid arguments. Usage: finalizer <logPath> <sdkVersion> <platform>");
  return;
}

string logPath = args[0];
string sdkVersion = args[1];
string platform = args[2];

Console.WriteLine($"{nameof(logPath)}: {logPath}");
Console.WriteLine($"{nameof(sdkVersion)}: {sdkVersion}");
Console.WriteLine($"{nameof(platform)}: {platform}");

try
{
  // Step 1: Parse and format SDK feature band version
  string featureBandVersion = ParseSdkVersion(sdkVersion);

  // Step 2: Check if SDK feature band is installed
  bool isInstalled = DetectSdk(featureBandVersion, platform);
  if (isInstalled)
  {
    Console.WriteLine($"SDK with feature band {featureBandVersion} is already installed.");
    return;
  }

  // Step 3: Remove dependent components if necessary
  bool restartRequired = RemoveDependent(featureBandVersion);
  if (restartRequired)
  {
    Console.WriteLine("A restart may be required after removing the dependent component.");
  }

  // Step 4: Delete workload records
  DeleteWorkloadRecords(featureBandVersion, platform);

  // Step 5: Clean up install state file
  RemoveInstallStateFile(featureBandVersion, platform);

  // Final reboot check
  if (restartRequired || IsRebootPending())
  {
    Console.WriteLine("A system restart is recommended to complete the operation.");
  }
  else
  {
    Console.WriteLine("Operation completed successfully. No restart is required.");
  }
}
catch (Exception ex)
{
  Console.WriteLine($"Error: {ex}");
}

static string ParseSdkVersion(string sdkVersion)
{
  var parts = sdkVersion.Split('.');
  if (parts.Length < 3)
    throw new ArgumentException("Invalid SDK version format.");

  if (!int.TryParse(parts[2], out int patch) || patch < 100)
    throw new ArgumentException("Invalid patch level in SDK version.");

  int featureBand = patch - (patch % 100);
  return $"{parts[0]}.{parts[1]}.{featureBand}";
}

static bool DetectSdk(string featureBandVersion, string platform)
{
  string registryPath = $@"SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\{platform}\sdk";
  using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registryPath))
  {
    if (key == null)
    {
      Console.WriteLine("SDK registry path not found.");
      return false;
    }

    foreach (var valueName in key.GetValueNames())
    {
      if (valueName.Contains(featureBandVersion))
      {
        Console.WriteLine($"SDK version detected: {valueName}");
        return true;
      }
    }
  }
  return false;
}

static bool RemoveDependent(string dependent)
{
    Installer.SetInternalUI(InstallUIOptions.Silent);

    // Open the installer dependencies registry key
    // This has to be an exhaustive search as we're not looking for a specific provider key, but for a specific dependent
    // that could be registered against any provider key.
    using var hkInstallerDependenciesKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\Installer\Dependencies", writable: true);
    if (hkInstallerDependenciesKey == null)
    {
        Console.WriteLine("Installer dependencies key does not exist.");
        return false; // No dependencies to remove
    }

    // Iterate over each provider key in the dependencies
    foreach (string providerKeyName in hkInstallerDependenciesKey.GetSubKeyNames())
    {
        Console.WriteLine($"Processing provider key: {providerKeyName}");

        using var hkProviderKey = hkInstallerDependenciesKey.OpenSubKey(providerKeyName, writable: true);
        if (hkProviderKey == null) continue;

        // Open the Dependents subkey
        using var hkDependentsKey = hkProviderKey.OpenSubKey("Dependents", writable: true);
        if (hkDependentsKey == null) continue;

        // Check if the dependent exists and continue if it does not
        string[] dependentsKeys = hkDependentsKey.GetSubKeyNames();
        bool dependentExists = false;

        foreach (string dependentsKeyName in dependentsKeys)
        {
            if (string.Equals(dependentsKeyName, dependent, StringComparison.OrdinalIgnoreCase))
            {
                dependentExists = true;
                break;
            }
        }

        if (!dependentExists)
        {
            continue; // Skip to the next provider key if the dependent does not exist
        }

        Console.WriteLine($"Dependent match found: {dependent}");

        // Attempt to remove the dependent key
        try
        {
            hkDependentsKey.DeleteSubKey(dependent);
            Console.WriteLine("Dependent deleted");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception while removing dependent key: {ex.Message}");
            return false;
        }

        // Check if any dependents are left
        if (hkDependentsKey.SubKeyCount == 0)
        {
            // No remaining dependents, handle product uninstallation
            try
            {
                string productCode = hkProviderKey.GetValue("ProductId").ToString();

                // Configure the product to be absent
                Installer.ConfigureProduct(productCode, 0, InstallState.Absent, "");
                Console.WriteLine("Product configured to absent successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling product configuration: {ex.Message}");
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

  using (RegistryKey key = Registry.LocalMachine.OpenSubKey(workloadKey, writable: true))
  {
    if (key != null)
    {
      key.DeleteSubKeyTree(featureBandVersion, throwOnMissingSubKey: false);
      Console.WriteLine($"Deleted workload records for '{featureBandVersion}'.");
    }
    else
    {
      Console.WriteLine("No workload records found to delete.");
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
    Console.WriteLine($"Deleted install state file: {installStatePath}");

    var dir = new DirectoryInfo(installStatePath).Parent;
    while (dir != null && dir.Exists && dir.GetFiles().Length == 0 && dir.GetDirectories().Length == 0)
    {
      dir.Delete();
      dir = dir.Parent;
    }
  }
  else
  {
    Console.WriteLine("Install state file does not exist.");
  }
}

static bool IsRebootPending()
{
  using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
  {
    if (key != null)
    {
      Console.WriteLine("Reboot is pending due to component-based servicing.");
      return true;
    }
  }

  using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager"))
  {
    var value = key?.GetValue("PendingFileRenameOperations");
    if (value != null)
    {
      Console.WriteLine("Pending file rename operations indicate a reboot is pending.");
      return true;
    }
  }

  Console.WriteLine("No reboot pending.");
  return false;
}
