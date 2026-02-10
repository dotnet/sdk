# Manually Testing Finalizer Changes

The finalizer can be partially tested in isolation by creating the necessary registry keys an installation would create. These can be easily configured on a VM or using Windows Sandbox. Otherwise, a full build of the installer is required along with the modified finalizer that results in a longer inner loop.

The finalizer will use the default hive, e.g., when compiled for x64, the finalizer will default to using the 64-bit registry hive.

### SDK Installation key

The installation key resides under the 32-bit hive, regardless of the OS architecture.

`REG ADD HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sdk /v 6.0.107 /t REG_DWORD /d 1 /reg:32`

### Workload Pack Records

Records related to workload packs are created by the pack installers. Testing changes to the removal process requires the packs to be installed since the finalizer relies on using specific data such as the workload pack installer's product code to remove the installation.

### Workload Records

A workload record entry can be created using the following command. The key resides in either hive depending on the SDK installation. Records are associated with an SDK feature band, e.g., 6.0.100.

`REG ADD HKLM\SOFTWARE\Microsoft\dotnet\InstalledWorkloads\Standalone\x64\6.0.100\wasm-tools`
