// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Management.Infrastructure;
using Microsoft.Management.Infrastructure.Serialization;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace Microsoft.DotNet.MsiInstallerTests
{
    internal class VirtualMachine : IDisposable
    {
        public string VMName { get; set; } = "Windows 11 dev environment";

        const string virtNamespace = @"root\virtualization\v2";

        public ITestOutputHelper Log { get; set; }

        private CimSession _session;

        public VirtualMachine(ITestOutputHelper log)
        {
            Log = log;
            _session = CimSession.Create(Environment.MachineName);
        }

        public CommandResult RunCommand(params string[] args)
        {
            throw new NotImplementedException();
        }

        public void CopyFile(string localSource, string vmDestination)
        {
            throw new NotImplementedException();
        }

        public void WriteFile(string vmDestination, string contents)
        {
            throw new NotImplementedException();
        }

        public RemoteDirectory GetRemoteDirectory(string path)
        {
            throw new NotImplementedException();
        }


        private CimInstance GetVMInstance()
        {
            return _session.QueryInstances(virtNamespace, "WQL", $"SELECT * FROM Msvm_ComputerSystem WHERE ElementName='{VMName}'").First();
        }

        public IEnumerable<(string id, string name)> GetSnapshots()
        {
            return GetSnapshotInstances().Select(s => ((string)s.CimInstanceProperties["ConfigurationID"].Value, (string)s.CimInstanceProperties["ElementName"].Value));
        }

        private IEnumerable<CimInstance> GetSnapshotInstances()
        {
            
            using var vm = GetVMInstance();
            var snapshots = _session.QueryInstances(virtNamespace, "WQL", $"SELECT * FROM Msvm_VirtualSystemSettingData WHERE VirtualSystemIdentifier='{vm.CimInstanceProperties["Name"].Value}' And IsSaved='True'").ToList();
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
            var snapshots = GetSnapshotInstances();

            var snapshot = snapshots.Single(s => s.CimInstanceProperties["ConfigurationID"].Value.ToString() == snapshotId);

            snapshot.CimInstanceProperties["ElementName"].Value = newName;


            var managementService = _session.QueryInstances(@"root\virtualization\v2", "WQL", "SELECT * FROM Msvm_VirtualSystemManagementService").Single();
            var modifyVmMethod = managementService.CimClass.CimClassMethods.Single(m => m.Name == "ModifySystemSettings");
            //foreach (var param in modifyVmMethod.Parameters)
            //{
            //    Log.WriteLine($"{param.Name}: {param.CimType}");
            //}

            CimSerializer serializer = CimSerializer.Create();
            var snapshotString = Encoding.Unicode.GetString(serializer.Serialize(snapshot, InstanceSerializationOptions.None));

            CimMethodParametersCollection cimMethodParameters = new CimMethodParametersCollection {
                CimMethodParameter.Create("SystemSettings", snapshotString, CimType.String, CimFlags.In)
            };

            var result = _session.InvokeMethod(virtNamespace, managementService, "ModifySystemSettings", cimMethodParameters);

            await WaitForJobSuccess(result);

        }

        public async Task CreateSnapshotAsync(string snapshotName)
        {
            //Log.WriteLine(Environment.MachineName);

            //CimSession session = GetCimSession(publicServer.HostName);
            //CimInstance vm = session.QueryInstances(@"root\virtualization\v2", "WQL", "SELECT * FROM Msvm_ComputerSystem WHERE Name='" + publicServer.MachineID + "'").FirstOrDefault();
            //var vms = session.QueryInstances(virtNamespace, "WQL", $"SELECT * FROM Msvm_ComputerSystem WHERE ElementName='{VMName}'");
            //var vms = session.QueryInstances(@"root\virtualization\v2", "WQL", "SELECT * FROM Msvm_ComputerSystem");
            //var vm = session.CreateInstance(@"root\virtualization\v2", "Msvm_VirtualSystemManagementService");
            //var vms = session.QueryInstances(@"root\virtualization\v2", "WQL", "SELECT * FROM Msvm_VirtualSystemManagementService");

            //foreach (var vm in vms)
            //{
            //    //Log.WriteLine(vm.ToString());
            //    //Log.WriteLine(vm.CimInstanceProperties["MachineID"].Value.ToString());
            //    Log.WriteLine(vm.CimInstanceProperties["ElementName"].Value.ToString());
            //    foreach (var prop in vm.CimInstanceProperties)
            //    {
            //        Log.WriteLine($"\t{prop.Name}: {prop.Value}");
            //    }
            //}

            using var targetVM = GetVMInstance();

            var existingSnapshots = GetSnapshots().ToDictionary(s => s.id, s => s.name);

            var snapshotService = _session.EnumerateInstances(virtNamespace, "Msvm_VirtualSystemSnapshotService").First();

            //var settingData = _session.CreateInstance(virtNamespace, new CimInstance("CIM_SettingData"));

            CimMethodParametersCollection cimMethodParameters = new CimMethodParametersCollection {
                CimMethodParameter.Create("AffectedSystem", targetVM, CimType.Reference, CimFlags.In),
                CimMethodParameter.Create("SnapshotType", 2, CimType.UInt16, CimFlags.In),
            };



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

            //Log.WriteLine($"Created snapshot {newSnapshot.name} ({newSnapshot.id})");

            await RenameSnapshotAsync(newSnapshot.id, snapshotName);

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

            var result = _session.InvokeMethod(virtNamespace, snapshotService, "ApplySnapshot", cimMethodParameters);

            await WaitForJobSuccess(result);

            await SetRunningAsync(true);
        }

        public async Task SetRunningAsync(bool running)
        {
            using var vm = GetVMInstance();

            bool isAlreadyRunning = (UInt16) vm.CimInstanceProperties["EnabledState"].Value == 2;
            if (running == isAlreadyRunning)
            {
                return;
            }

            var methodParameters = new CimMethodParametersCollection()
            {
                CimMethodParameter.Create("RequestedState", (UInt16) (running ? 2 : 4), CimFlags.In)
            };

            var result = _session.InvokeMethod(virtNamespace, vm, "RequestStateChange", methodParameters);

            await WaitForJobSuccess(result);
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
                    throw new Exception("Job failed");
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


        private class CimObserver<T> : IObserver<T>
        {
            TaskCompletionSource<T> _tcs;
            Management.Infrastructure.Generic.CimAsyncResult<T> _result;

            public Task<T> Task => _tcs.Task;

            public CimObserver(Management.Infrastructure.Generic.CimAsyncResult<T> result)
            {
                _tcs = new TaskCompletionSource<T>();
                _result = result;
                _result.Subscribe(this);
            }

            public void OnCompleted()
            {
            }
            public void OnError(Exception error)
            {
                _tcs.SetException(error);
            }
            public void OnNext(T value)
            {
                _tcs.SetResult(value);
            }
        }

    }
}
