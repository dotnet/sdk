# VM-based installation tests

On Windows, when the .NET SDK is installed either via the standalone installer or by Visual Studio, it is installed to the Program Files folder.  In both cases it uses MSIs under the hood, so we call this an MSI-based install (as opposed to a file-based install which consists of unzipping or copying the .NET SDK files to an arbitrary folder).

For an MSI-based .NET SDK install, .NET SDK workloads are also installed using MSIs.  This modifies global machine state, which makes it hard to write automated tests for MSI-based workload operations.

To address this, the MSI Installation tests use a Virtual Machine as the target environment to install the .NET SDK and workloads.  APIs to run commands on the Virtual Machine or to inspect its file system are available and are similar to the test APIs used in other tests in the repo.

Because installation actions can be fairly slow, the test infrastructure uses VM snapshots (also called checkpoints) to avoid repeating an action that was already run.  Thus, if multiple tests have the same setup steps, those steps won't be repeated for each test, rather the correct snapshot will be applied when needed.  As tests are run, a tree of states (with corresponding snapshots) and actions to transition between them are built up from the initial state.  This tree of states is saved across test runs, so if nothing has changed then running a test a second time should complete very quickly, as all of the results of the test actions were already recorded.

## Setting up a VM for running tests

- [Enable Hyper-V](https://learn.microsoft.com/en-us/virtualization/hyper-v-on-windows/quick-start/enable-hyper-v)
- Create a Hyper-V Virtual Machine.  A simple way to do this is to use "Quick Create" and choose "Windows 11 dev environment".  This currently creates a VM with Visual Studio 17.7 and .NET SDK 7.0.401 installed.  For now the tests are written to take this into account, in the future we may want to start with a clean installation of Windows.