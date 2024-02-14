# VM-based installation tests

On Windows, when the .NET SDK is installed either via the standalone installer or by Visual Studio, it is installed to the Program Files folder.  In both cases it uses MSIs under the hood, so we call this an MSI-based install (as opposed to a file-based install which consists of unzipping or copying the .NET SDK files to an arbitrary folder).

For an MSI-based .NET SDK install, .NET SDK workloads are also installed using MSIs.  This modifies global machine state, which makes it hard to write automated tests for MSI-based workload operations.

To address this, the MSI Installation tests use a Virtual Machine as the target environment to install the .NET SDK and workloads.  APIs to run commands on the Virtual Machine or to inspect its file system are available and are similar to the test APIs used in other tests in the repo.

Because installation actions can be fairly slow, the test infrastructure uses VM snapshots (also called checkpoints) to avoid repeating an action that was already run.  Thus, if multiple tests have the same setup steps, those steps won't be repeated for each test, rather the correct snapshot will be applied when needed.  As tests are run, a tree of states (with corresponding snapshots) and actions to transition between them are built up from the initial state.  This tree of states is saved across test runs, so if nothing has changed then running a test a second time should complete very quickly, as all of the results of the test actions were already recorded.

## Setting up a VM for running tests

The main requirements for running the tests are:

- A Hyper-V Virtual Machine
- Access to the VM via the admin share (`\\TestVM\c$`)
- [PsExec](https://learn.microsoft.com/en-us/sysinternals/downloads/psexec) on the PATH of the host machine
- "Remote Service Management" enabled in firewall settings inside the VM (this avoids a delay of 15-30 seconds for each command that is run)
- Tests need to be run with admin priviledges, which are required to use the WMI APIs to control VMs

Detailed steps:

- [Enable Hyper-V](https://learn.microsoft.com/en-us/virtualization/hyper-v-on-windows/quick-start/enable-hyper-v)
- For the tests to remotely control the VM, your host computer needs to be able to access the VM over the network.  To enable this, you need to create a virtual switch for the VM which will also be shared by the host PC.  In Hyper-V Manager, go to Virtual Switch Manager.  Create a new Virtual Switch connected to your external network adapter, and check the box that says "Allow management operating system to share this network adapter."
- Create a Hyper-V Virtual machine
  - You will need to choose a name for the virtual machine (used by the host) and a machine name for it when setting up Windows inside the VM.  You can choose whatever names you want, but "Test VM" and "TestVM" are good defaults.
  - You can download a Windows 11 .iso here: https://www.microsoft.com/en-us/software-download/windows11/
  - In the networking configuration for the VM, select the Virtual Switch you created
  - Under Security, check "Enable Trusted Platform Module" (and possibly the "Encrypt state..." checkbox under it), which is required to install Windows 11
  - Start the VM and install Windows
    - Probably you don't want to sign on to the test VM with a Microsoft account.  Setting up with a local account is tricky, but you can do so with these steps: https://www.tomshardware.com/how-to/install-windows-11-without-microsoft-account
  - In the VM settings in Hyper-V manager, enable all the integration services (so you can copy/paste files to the VM, for example)
- In network settings inside the VM, switch the network connection type to Private Network, and turn on Network discovery and File and printer sharing.
- Inside the VM, set the HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System\LocalAccountTokenFilterPolicy registry value to 1 ([reference](https://learn.microsoft.com/en-us/troubleshoot/windows-server/windows-security/user-account-control-and-remote-restriction)).  This will allow you to access the admin share (`\\TestVM\c$`).
- Browse to the admin share in File Explorer to confirm it's working.  You will need to enter the username and password for the VM.  Select the option to save the login information.  This will allow the tests to access the VM.
- Inside the VM, go to "Allow an app through Windows Firewall", and add "Remote Service Management" to the list of allowed apps and features.  This allows PsExec to launch commands quickly, otherwise there is a delay of around 15-30 seconds for each command that is run.
- Download PSTools, extract them somewhere, and add that folder to your PATH.  Run something like `psexec \\TestVM cmd /c dir c:\` to verify that PsExec can run commands on the VM.  The command should complete in less than a second, if it takes longer then the Remote Service Management firewall rule is probably not enabled correctly.
- Create a `C:\SdkTesting` folder inside the VM.  Copy the standalone installer for the baseline version of the SDK used to test (for example `dotnet-sdk-8.0.100-win-x64.exe`) to that folder
- Recommended:
  - Install [Visual Studio Remote Tools](https://learn.microsoft.com/visualstudio/debugger/remote-debugging?view=vs-2022#download-and-install-the-remote-tools) in the VM so that if you need to debug the SDK you can do so.  Run the remote tools and allow it through the firewall
  - Install any other tools or set up any other settings you would like on the VM to help investigate failures.  It's easier if you do this all as part of the initial state, otherwise each time you run a test the state will be reset so you may end up repeating the same actions multiple times.
- Create a snapshot (checkpoint) of the VM in Hyper-V manager.  Rename the snapshot so that the name contains "Test start".  There needs to be exactly one snapshot with "Test start" in its name, which the tests will use as the initial root state.

## Test settings

The tests can read settings from a `VMTestSettings.json` file, and they store the VM State tree in a `VMState.json` file.  Both of these go in the current directory, which when running tests in Visual Studio will be `bin\Tests\dotnet-MsiInstallation.Tests\Debug`.  The `VMTestSettings.json` file can have the following values:

- **VMName** - The name of the VM to use for testing.  This is the name displayed in Hyper-V manager.  This value doesn't need to be set if there is only one Virtual Machine on the host.
- **VMMachineName** - The machine name used by the VM's operating system.  This is what is used to browse to the machine in File Explorer, for example `\\TestVM`.  If this isn't specified, the tests will use the VM Name with any spaces removed for the VM machine name.
- **SdkInstallerVersion** - The version of the SDK to install for testing.  The installer for this version needs to be copied to `C:\SdkTesting` inside the VM.
- **ShouldTestStage2** - If set to true (which is the default), then the tests will copy the SDK implementation binaries to the installed SDK folder inside the VM.  Basically, you should leave this set to true if you want to test local changes to the SDK, and you should set it to false if you want to test the SDK specified in `SdkInstallerVersion`.

If you want to change the snapshot used for the initial test state, in addition to renaming the snapshots so that the new one has "Test Start" in its name, you will need to delete the `VMState.json` file, as otherwise the root test state will be read from it.

## Notes

Hyper-V supports a maximum of 50 snapshots per VM.  If you make changes to the SDK and re-run tests, new snapshots will be created for the newly deployed stage 2 SDK.  You may need to delete the older snapshots in order avoid hitting the snapshot limit.  Also, if you want to force a test to run a command instead of using the cached result, you can delete the corresponding snapshot.