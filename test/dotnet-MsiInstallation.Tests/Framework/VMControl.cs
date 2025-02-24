// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Installer.Windows;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Serialization;

namespace Microsoft.DotNet.MsiInstallerTests.Framework
{
    internal class VMControl : IDisposable
    {
        public string VMName { get; }
        public string VMMachineName { get; }

        const string virtNamespace = @"root\virtualization\v2";

        string _psExecPath;

        public ITestOutputHelper Log { get; }

        private CimSession _session;
        private CimInstance VMInstance
        {
            get
            {
                //  Query WMI for the VM instance each time so that the state is always up to date
                return _session.QueryInstances(virtNamespace, "WQL", $"SELECT * FROM Msvm_ComputerSystem WHERE ElementName='{VMName}'").Single();
            }
        }

        public VMControl(ITestOutputHelper log, string vMName, string vMMachineName)
        {
            if (!WindowsUtils.IsAdministrator())
            {
                throw new Exception("Must be running as admin to control virtual machines");
            }

            Log = log;
            VMName = vMName;
            VMMachineName = vMMachineName;

            _session = CimSession.Create(Environment.MachineName);

            if (!ToolsetInfo.TryResolveCommand("PsExec", out _psExecPath))
            {
                throw new Exception("Couldn't find PsExec on PATH");
            }
        }

        public static List<string> GetVirtualMachines(ITestOutputHelper log)
        {
            if (!WindowsUtils.IsAdministrator())
            {
                throw new Exception("Must be running as admin to control virtual machines");
            }

            using var session = CimSession.Create(Environment.MachineName);

            var vms = session.QueryInstances(virtNamespace, "WQL", "SELECT * FROM Msvm_ComputerSystem WHERE Caption='Virtual Machine'").ToList();

            List<string> vmNames = new List<string>();

            foreach (var vm in vms)
            {
                vmNames.Add((string)vm.CimInstanceProperties["ElementName"].Value);
            }

            return vmNames;
        }

        public CommandResult RunCommandOnVM(string[] args, string workingDirectory = null)
        {
            var remoteCommand = new RemoteCommand(Log, VMMachineName, _psExecPath, workingDirectory, args);

            for (int i = 0; i < 3; i++)
            {
                var result = remoteCommand.Execute();
                if (result.ExitCode != 6 && !result.StdErr.Contains("The handle is invalid"))
                {
                    return result;
                }
                Log.WriteLine("PsExec failed, retrying...");
                Thread.Sleep(500);
            }

            throw new Exception(@$"PsExec failed, make sure VM is running and can be accessed via \\{VMMachineName}");
        }

        public IEnumerable<(string id, string name)> GetSnapshots()
        {
            return GetSnapshotInstances().Select(s => ((string)s.CimInstanceProperties["ConfigurationID"].Value, (string)s.CimInstanceProperties["ElementName"].Value));
        }

        private IEnumerable<CimInstance> GetSnapshotInstances()
        {
            //  Note: Not querying for IsSaved='True' here, as this value was false for snapshots in at least one case
            var snapshots = _session.QueryInstances(virtNamespace, "WQL", $"SELECT * FROM Msvm_VirtualSystemSettingData WHERE VirtualSystemIdentifier='{VMInstance.CimInstanceProperties["Name"].Value}'").ToList();

            return snapshots;
        }

        public async Task RenameSnapshotAsync(string snapshotId, string newName)
        {
            if (newName.Length >= 100)
            {
                newName = newName.Substring(0, 96) + "...";
            }

            var snapshots = GetSnapshotInstances();

            var managementService = _session.QueryInstances(@"root\virtualization\v2", "WQL", "SELECT * FROM Msvm_VirtualSystemManagementService").Single();
            var modifyVmMethod = managementService.CimClass.CimClassMethods.Single(m => m.Name == "ModifySystemSettings");

            var snapshot = snapshots.Single(s => s.CimInstanceProperties["ConfigurationID"].Value.ToString() == snapshotId);

            snapshot.CimInstanceProperties["ElementName"].Value = newName;

            Log.WriteLine("Renaming snapshot " + snapshotId + " to " + newName);

            CimSerializer serializer = CimSerializer.Create();
            var snapshotString = Encoding.Unicode.GetString(serializer.Serialize(snapshot, InstanceSerializationOptions.None));

            CimMethodParametersCollection cimMethodParameters = new CimMethodParametersCollection {
                CimMethodParameter.Create("SystemSettings", snapshotString, CimType.String, CimFlags.In)
            };

            var result = _session.InvokeMethod(virtNamespace, managementService, "ModifySystemSettings", cimMethodParameters);

            await WaitForJobSuccess(result);
        }

        public async Task<string> CreateSnapshotAsync(string snapshotName)
        {
            var existingSnapshots = GetSnapshots().ToDictionary(s => s.id, s => s.name);

            var snapshotService = _session.EnumerateInstances(virtNamespace, "Msvm_VirtualSystemSnapshotService").First();

            CimMethodParametersCollection cimMethodParameters = new CimMethodParametersCollection {
                CimMethodParameter.Create("AffectedSystem", VMInstance, CimType.Reference, CimFlags.In),
                CimMethodParameter.Create("SnapshotType", 2, CimType.UInt16, CimFlags.In),
            };


            Log.WriteLine("Creating snapshot " + snapshotName);

            var result = _session.InvokeMethod(virtNamespace, snapshotService, "CreateSnapshot", cimMethodParameters);

            await WaitForJobSuccess(result);

            var newSnapshot = GetSnapshots().Single(s => !existingSnapshots.ContainsKey(s.id));



            if (!string.IsNullOrEmpty(snapshotName))
            {
                await RenameSnapshotAsync(newSnapshot.id, snapshotName);
            }

            Log.WriteLine($"Created snapshot {snapshotName} ({newSnapshot.id})");

            return newSnapshot.id;
        }

        public async Task ApplySnapshotAsync(string snapshotId)
        {
            if (!await ApplySnapshotIfExistsAsync(snapshotId))
            {
                throw new Exception("Snapshot not found: " + snapshotId);
            }
        }

        public async Task<bool> ApplySnapshotIfExistsAsync(string snapshotId)
        {
            var snapshots = GetSnapshotInstances();

            var snapshot = snapshots.SingleOrDefault(s => s.CimInstanceProperties["ConfigurationID"].Value.ToString() == snapshotId);

            if (snapshot == null)
            {
                return false;
            }

            await SetRunningAsync(false);

            var snapshotService = _session.EnumerateInstances(virtNamespace, "Msvm_VirtualSystemSnapshotService").First();

            CimMethodParametersCollection cimMethodParameters = new CimMethodParametersCollection
            {
                CimMethodParameter.Create("Snapshot", snapshot, CimType.Reference, CimFlags.In),
            };

            Log.WriteLine($"Applying snapshot {snapshot.CimInstanceProperties["ElementName"].Value} ({snapshot.CimInstanceProperties["ConfigurationID"].Value})");

            var result = _session.InvokeMethod(virtNamespace, snapshotService, "ApplySnapshot", cimMethodParameters);

            await WaitForJobSuccess(result);

            await SetRunningAsync(true);

            return true;
        }

        public async Task SetRunningAsync(bool running)
        {
            VMEnabledState getCurrentState()
            {
                var state = (VMEnabledState)(ushort)VMInstance.CimInstanceProperties["EnabledState"].Value;
                return state;
            }

            var targetState = running ? VMEnabledState.Enabled : VMEnabledState.Disabled;

            if (getCurrentState() == targetState)
            {
                return;
            }

            Log.WriteLine($"Changing EnabledState from {getCurrentState()} to {targetState}");

            var methodParameters = new CimMethodParametersCollection()
            {
                CimMethodParameter.Create("RequestedState", (ushort) targetState, CimFlags.In)
            };


            var result = _session.InvokeMethod(virtNamespace, VMInstance, "RequestStateChange", methodParameters);

            await WaitForJobSuccess(result);

            for (int i = 0; i < 10 && getCurrentState() != targetState; i++)
            {
                Log.WriteLine("Waiting for state change...");
                await Task.Delay(250);
            }
        }

        private static bool IsRunning(JobState jobState)
        {
            return jobState == JobState.New || jobState == JobState.Starting || jobState == JobState.Running || jobState == JobState.Suspended || jobState == JobState.ShuttingDown;
        }

        private async Task WaitForJobSuccess(CimMethodResult result)
        {
            if ((uint)result.ReturnValue.Value == 4096)
            {
                CimInstance job = (CimInstance)result.OutParameters["Job"].Value;
                job = _session.GetInstance(virtNamespace, job);
                while (IsRunning((JobState)(ushort)job.CimInstanceProperties["JobState"].Value))
                {
                    await Task.Delay(100);
                    job = _session.GetInstance(job.CimSystemProperties.Namespace, job);
                }

                var jobState = (JobState)(ushort)job.CimInstanceProperties["JobState"].Value;
                if (jobState != JobState.Completed && jobState != JobState.CompletedWithWarnings)
                {
                    Log.WriteLine("Job failed: " + jobState);

                    string exceptionText = "Job failed: " + jobState;

                    try
                    {
                        exceptionText += Environment.NewLine +
                            "ErrorCode: " + job.CimInstanceProperties["ErrorCode"].Value + Environment.NewLine +
                            "ErrorDescription: " + job.CimInstanceProperties["ErrorDescription"].Value;
                    }
                    catch { }

                    throw new Exception(exceptionText);
                }
            }
            else
            {
                if (result.ReturnValue.Value is uint returnValue)
                {
                    if (returnValue != 0)
                    {
                        throw new Exception($"Job failed with return value {returnValue}");
                    }
                }
                else
                {
                    throw new Exception($"Job failed with return value {result.ReturnValue.Value}");
                }
            }
        }

        public void Dispose()
        {
            VMInstance.Dispose();
            _session.Dispose();
        }

        enum JobState
        {
            New = 2,
            Starting = 3,
            Running = 4,
            Suspended = 5,
            ShuttingDown = 6,
            Completed = 7,
            Terminated = 8,
            Killed = 9,
            Exception = 10,
            CompletedWithWarnings = 32768
        }

        enum VMEnabledState : ushort
        {
            Unknown = 0,
            Other = 1,
            Enabled = 2,
            Disabled = 3,
            ShuttingDown = 4,
            NotApplicable = 5,
            EnabledButOffline = 6,

        }

        class RemoteCommand : TestCommand
        {
            string _targetMachineName;
            string _psExecPath;
            string _workingDirectory;


            public RemoteCommand(ITestOutputHelper log, string targetMachineName, string psExecPath, string workingDirectory, string[] args)
                : base(log)
            {
                _targetMachineName = targetMachineName;
                _psExecPath = psExecPath;
                _workingDirectory = workingDirectory;

                Arguments.Add("-nobanner");
                Arguments.Add($@"\\{_targetMachineName}");

                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    Arguments.Add("-w");
                    Arguments.Add(workingDirectory);
                }

                Arguments.AddRange(args);
            }

            protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
            {
                var sdkCommandSpec = new SdkCommandSpec()
                {
                    FileName = _psExecPath,
                    Arguments = args.ToList(),
                    WorkingDirectory = WorkingDirectory,
                };
                return sdkCommandSpec;
            }
        }

    }
}
