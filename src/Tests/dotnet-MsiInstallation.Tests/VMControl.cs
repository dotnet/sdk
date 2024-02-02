// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Installer.Windows;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Serialization;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace Microsoft.DotNet.MsiInstallerTests
{
    internal class VMControl : IDisposable
    {
        public string VMName { get; set; } = "Windows 11 dev environment";
        public string VMMachineName { get; set; } = "dsp-vm";
        public string PsExecPath = @"C:\Users\Daniel\Downloads\PSTools\PsExec.exe";

        const string virtNamespace = @"root\virtualization\v2";

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

        public VMControl(ITestOutputHelper log)
        {
            if (!WindowsUtils.IsAdministrator())
            {
                throw new Exception("Must be running as admin to control virtual machines");
            }

            Log = log;
            _session = CimSession.Create(Environment.MachineName);
        }

        public CommandResult RunCommandOnVM(params string[] args)
        {
            var remoteCommand = new RemoteCommand(Log, VMMachineName, PsExecPath, args);

            for (int i=0; i<3; i++)
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
            var snapshots = _session.QueryInstances(virtNamespace, "WQL", $"SELECT * FROM Msvm_VirtualSystemSettingData WHERE VirtualSystemIdentifier='{VMInstance.CimInstanceProperties["Name"].Value}' And IsSaved='True'").ToList();
            //Log.WriteLine("Snapshot count: " + snapshots.Count);
            //foreach (var snapshot in snapshots)
            //{
            //    Log.WriteLine(snapshot.CimInstanceProperties["ElementName"].Value.ToString());
            //    //Log.WriteLine(snapshot.ToString());
            //    //foreach (var prop in snapshot.CimInstanceProperties)
            //    //{
            //    //    Log.WriteLine($"\t{prop.Name}: {prop.Value}");
            //    //}
            //}

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
            //foreach (var param in modifyVmMethod.Parameters)
            //{
            //    Log.WriteLine($"{param.Name}: {param.CimType}");
            //}

            var snapshot = snapshots.Single(s => s.CimInstanceProperties["ConfigurationID"].Value.ToString() == snapshotId);

            snapshot.CimInstanceProperties["ElementName"].Value = newName;

            Log.WriteLine("Renaming snapshot " + snapshotId + " to " + newName);
            //foreach (var prop in snapshot.CimInstanceProperties)
            //{
            //    Log.WriteLine($"\t{prop.Name}: {prop.Value}");
            //}

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

            //var settingData = _session.CreateInstance(virtNamespace, new CimInstance("CIM_SettingData"));

            CimMethodParametersCollection cimMethodParameters = new CimMethodParametersCollection {
                CimMethodParameter.Create("AffectedSystem", VMInstance, CimType.Reference, CimFlags.In),
                CimMethodParameter.Create("SnapshotType", 2, CimType.UInt16, CimFlags.In),
            };


            Log.WriteLine("Creating snapshot " + snapshotName);

            var result = _session.InvokeMethod(virtNamespace, snapshotService, "CreateSnapshot", cimMethodParameters);

            await WaitForJobSuccess(result);

            //var updatedSnapshots = GetSnapshots().ToDictionary(s => s.id, s => s.name);
            //foreach (var snapshot in updatedSnapshots)
            //{
            //    if (!existingSnapshots.ContainsKey(snapshot.Key))
            //    {
            //        Log.WriteLine($"Created snapshot {snapshot.Value} ({snapshot.Key})");
            //    }
            //}

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
            await SetRunningAsync(false);

            var snapshots = GetSnapshotInstances();

            var snapshot = snapshots.Single(s => s.CimInstanceProperties["ConfigurationID"].Value.ToString() == snapshotId);

            var snapshotService = _session.EnumerateInstances(virtNamespace, "Msvm_VirtualSystemSnapshotService").First();

            CimMethodParametersCollection cimMethodParameters = new CimMethodParametersCollection
            {
                CimMethodParameter.Create("Snapshot", snapshot, CimType.Reference, CimFlags.In),
            };

            Log.WriteLine($"Applying snapshot {snapshot.CimInstanceProperties["ElementName"].Value} ({snapshot.CimInstanceProperties["ConfigurationID"].Value})");

            var result = _session.InvokeMethod(virtNamespace, snapshotService, "ApplySnapshot", cimMethodParameters);

            await WaitForJobSuccess(result);

            await SetRunningAsync(true);
        }

        public async Task SetRunningAsync(bool running)
        {
            VMEnabledState getCurrentState()
            {
                var state = (VMEnabledState) (UInt16)VMInstance.CimInstanceProperties["EnabledState"].Value;
                Log.WriteLine("Current EnabledState: " + state);
                return state;
            }

            var targetState = running ? VMEnabledState.Enabled : VMEnabledState.Disabled;

            if (getCurrentState() == targetState)
            {
                return;
            }

            //bool isAlreadyRunning = getCurrentState() == 2;
            //if (running == isAlreadyRunning)
            //{
            //    return;
            //}

            Log.WriteLine("Setting EnabledState to " + targetState);

            var methodParameters = new CimMethodParametersCollection()
            {
                CimMethodParameter.Create("RequestedState", (UInt16) targetState, CimFlags.In)
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
                while (IsRunning((JobState)(UInt16)job.CimInstanceProperties["JobState"].Value))
                {
                    await Task.Delay(100);
                    job = _session.GetInstance(job.CimSystemProperties.Namespace, job);
                }

                var jobState = (JobState)(UInt16)job.CimInstanceProperties["JobState"].Value;
                if (jobState != JobState.Completed && jobState != JobState.CompletedWithWarnings)
                {
                    Log.WriteLine("Job failed: " + jobState);
                    //foreach (var prop in job.CimInstanceProperties)
                    //{
                    //    Log.WriteLine($"\t{prop.Name}: {prop.Value}");
                    //}

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
                if (result.ReturnValue.Value is UInt32 returnValue)
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

        enum VMEnabledState : UInt16
        {
            Unknown = 0,
            Other = 1,
            Enabled = 2,
            Disabled = 3,
            ShuttingDown = 4,
            NotApplicable = 5,
            EnabledButOffline = 6,
            //NoContact = 7,
            //LostCommunication = 8,
            //DisabledNoRedundancy = 9,
            //InTest = 10,
            //Deferred = 11,
            //Quiesce = 12,
            //Starting = 13,
            //Snapshotting = 14,
            //Saving = 15,
            //Stopping = 16,
            //Pausing = 17,
            //Resuming = 18,
            //FastSnapshotting = 19,
            //FastSaving = 20,
            //StartingUp = 21,
            //PoweringDown = 22,
            //PoweringUp = 23,
            //SavingState = 24,
            //RestoringState = 25,
            //Paused = 26,
            //Resumed = 27,
            //Suspended = 28,
            //StartingUpService = 29,
            //UndoingSnapshot = 30,
            //ImportingSnapshot = 31,
            //ExportingSnapshot = 32,
            //MergingSnapshot = 33,
            //RemovingSnapshot = 34,
            //ApplyingSnapshot = 35,
            //Killing = 36,
            //StoppingService = 37,
            //SuspendedWithSavedState = 38,
            //StartingUpWithSavedState = 39,
            //InService = 40,
            //NoRecovery = 41,
            //Last = 42
        }

        //private class CimObserver<T> : IObserver<T>
        //{
        //    TaskCompletionSource<T> _tcs;
        //    Management.Infrastructure.Generic.CimAsyncResult<T> _result;

        //    public Task<T> Task => _tcs.Task;

        //    public CimObserver(Management.Infrastructure.Generic.CimAsyncResult<T> result)
        //    {
        //        _tcs = new TaskCompletionSource<T>();
        //        _result = result;
        //        _result.Subscribe(this);
        //    }

        //    public void OnCompleted()
        //    {
        //    }
        //    public void OnError(Exception error)
        //    {
        //        _tcs.SetException(error);
        //    }
        //    public void OnNext(T value)
        //    {
        //        _tcs.SetResult(value);
        //    }
        //}

        class RemoteCommand : TestCommand
        {
            string _targetMachineName;
            string _psExecPath;


            public RemoteCommand(ITestOutputHelper log, string targetMachineName, string psExecPath, params string[] args)
                : base(log)
            {
                _targetMachineName = targetMachineName;
                _psExecPath = psExecPath;

                Arguments.Add("-nobanner");
                Arguments.Add($@"\\{_targetMachineName}");
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
