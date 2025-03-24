// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.MsiInstallerTests.Framework
{
    class VirtualMachine : IDisposable
    {
        private static VirtualMachine s_Instance;
        private static object s_Lock = new object();

        ITestOutputHelper Log { get; }
        public VMControl VMControl { get; }

        public VMTestSettings VMTestSettings { get; }

        VMState _vmState;
        VMStateTree _currentState;
        VMStateTree _currentAppliedState;

        string _stateFile;

        //  Whether we should trust the IsReadOnly property of the action.  If not, then we will re-apply the previous snapshot after the action runs to make sure the state hasn't been polluted.
        public bool TrustIsReadOnly { get; set; } = true;

        public VirtualMachine(ITestOutputHelper log)
        {
            lock (s_Lock)
            {
                if (s_Instance != null)
                {
                    throw new Exception(nameof(VirtualMachine) + " already created.  This can be caused by running multiple tests which use the VM in parallel, which is not supported.");
                }
                s_Instance = this;
            }

            Log = log;

            var testSettingsFile = Path.Combine(Environment.CurrentDirectory, "VMTestSettings.json");
            if (File.Exists(testSettingsFile))
            {
                string json = File.ReadAllText(testSettingsFile);
                VMTestSettings = JsonSerializer.Deserialize<VMTestSettings>(json);
            }
            else
            {
                VMTestSettings = new();
            }

            if (string.IsNullOrEmpty(VMTestSettings.VMName))
            {
                var virtualMachineNames = VMControl.GetVirtualMachines(Log);

                if (virtualMachineNames.Count == 0)
                {
                    throw new Exception("No virtual machines found");
                }
                else if (virtualMachineNames.Count == 1)
                {
                    VMTestSettings.VMName = virtualMachineNames[0];
                }
                else if (virtualMachineNames.Count > 1)
                {
                    throw new Exception($"Multiple virtual machines found. Use {testSettingsFile} to specify which VM should be used for tests.");
                }
            }

            if (string.IsNullOrEmpty(VMTestSettings.VMMachineName))
            {
                VMTestSettings.VMMachineName = VMTestSettings.VMName.Replace(" ", "");
            }

            VMControl = new VMControl(log, VMTestSettings.VMName, VMTestSettings.VMMachineName);


            _stateFile = Path.Combine(Environment.CurrentDirectory, "VMState.json");

            //  Load root state from file, if it exists
            if (File.Exists(_stateFile))
            {
                string json = File.ReadAllText(_stateFile);
                _vmState = JsonSerializer.Deserialize<SerializableVMState>(json, GetSerializerOptions()).ToVMState();
            }
            else
            {
                _vmState = new VMState();
            }

            //  Determine test start state
            var snapshots = VMControl.GetSnapshots();
            var testStartSnapshots = snapshots.Where(s => s.name.Contains("Test start", StringComparison.OrdinalIgnoreCase)).ToList();
            if (testStartSnapshots.Count == 0)
            {
                throw new Exception("No test start snapshots found");
            }
            else if (testStartSnapshots.Count > 1)
            {
                foreach (var snapshot in testStartSnapshots)
                {
                    Log.WriteLine(snapshot.id + ": " + snapshot.name);
                }
                throw new Exception("Multiple test start snapshots found");
            }

            _vmState.DefaultRootState = testStartSnapshots[0].name;
            if (!_vmState.VMStates.ContainsKey(_vmState.DefaultRootState))
            {
                _vmState.VMStates[_vmState.DefaultRootState] = new VMStateTree()
                {
                    SnapshotId = testStartSnapshots[0].id,
                    SnapshotName = testStartSnapshots[0].name
                };
            }
            _currentState = _vmState.GetRootState();

            TrimMissingSnapshots();
        }
        public void Dispose()
        {
            string json = JsonSerializer.Serialize(_vmState.ToSerializable(), GetSerializerOptions());
            File.WriteAllText(_stateFile, json);

            VMControl.Dispose();

            lock (s_Lock)
            {
                s_Instance = null;
            }
        }

        JsonSerializerOptions GetSerializerOptions()
        {
            return new JsonSerializerOptions()
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public void TrimMissingSnapshots()
        {
            var snapshotIds = VMControl.GetSnapshots().Select(s => s.id).ToHashSet();

            foreach (var state in _vmState.VMStates.Values.ToList())
            {
                if (!snapshotIds.Contains(state.SnapshotId))
                {
                    _vmState.VMStates.Remove(state.SnapshotId);
                }
                else
                {
                    Recurse(state);
                }
            }

            void Recurse(VMStateTree node)
            {
                var nodesToRemove = node.Actions.Where(a => !snapshotIds.Contains(a.Value.resultingState.SnapshotId)).ToList();
                foreach (var nodeToRemove in nodesToRemove)
                {
                    Log.WriteLine($"Removing missing snapshot from tree: {nodeToRemove.Value.resultingState.SnapshotName}");
                    node.Actions.Remove(nodeToRemove.Key);
                }

                foreach (var result in node.Actions.Select(a => a.Value.resultingState))
                {
                    Recurse(result);
                }
            }
        }

        public void SetCurrentState(string stateName)
        {
            if (_vmState.VMStates.TryGetValue(stateName, out var state))
            {
                _currentState = state;
            }
            else
            {
                var snapshots = VMControl.GetSnapshots();
                var matchingSnapshots = snapshots.Where(s => s.name.Equals(stateName, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matchingSnapshots.Count == 0)
                {
                    throw new Exception($"No snapshot found with name {stateName}");
                }
                else if (matchingSnapshots.Count > 1)
                {
                    throw new Exception($"Multiple snapshots found with name {stateName}");
                }
                else
                {
                    var newState = new VMStateTree()
                    {
                        SnapshotId = matchingSnapshots[0].id,
                        SnapshotName = matchingSnapshots[0].name,
                    };
                    _vmState.VMStates[stateName] = newState;
                    _currentState = newState;
                }
            }
        }

        public VMRunAction CreateRunCommand(params string[] args)
        {
            return new VMRunAction(this, args.ToList());
        }

        public VMCopyFileAction CopyFile(string localSource, string vmDestination)
        {
            return new VMCopyFileAction(this)
            {
                LocalSource = localSource,
                TargetPath = vmDestination,
            };
        }

        public VMCopyFolderAction CopyFolder(string localSource, string vmDestination)
        {
            return new VMCopyFolderAction(this)
            {
                LocalSource = localSource,
                TargetPath = vmDestination,
            };
        }

        public VMWriteFileAction WriteFile(string vmDestination, string contents)
        {
            return new VMWriteFileAction(this)
            {
                TargetPath = vmDestination,
                FileContents = contents,
            };
        }

        public VMGroupedAction CreateActionGroup(string name, params VMAction[] actions)
        {
            return new VMGroupedAction(this)
            {
                ExplicitDescription = name,
                Actions = actions.ToList(),
            };
        }

        public RemoteFile GetRemoteFile(string path, bool mustExist = false)
        {
            return new VMRemoteFile(this, path, mustExist);
        }

        public RemoteDirectory GetRemoteDirectory(string path, bool mustExist = false)
        {
            return new VMRemoteDirectory(this, path, mustExist);
        }

        public VMSnapshot CreateSnapshot()
        {
            return new VMSnapshot(this, _currentState);
        }

        void SyncToCurrentState()
        {
            if (_currentAppliedState != _currentState)
            {
                VMControl.ApplySnapshotAsync(_currentState.SnapshotId).Wait();
                _currentAppliedState = _currentState;
            }
        }

        void LogActionResult(SerializedVMAction action, VMActionResult result)
        {
            if (action.Type == VMActionType.RunCommand)
            {
                TestCommand.LogCommandResult(Log, result.ToCommandResult());
            }
            else if (action.Type == VMActionType.ActionGroup && result.GroupedResults != null)
            {
                for (int i = 0; i < result.GroupedResults.Count; i++)
                {
                    LogActionResult(action.Actions[i], result.GroupedResults[i]);
                }
            }
        }

        //  Runs a command if necessary, or returns previously recorded result.  Handles syncing to the correct state, creating a new snapshot, etc.
        public VMActionResult Apply(SerializedVMAction action)
        {
            if (action.IsReadOnly)
            {
                if (_currentState.ReadOnlyActions.TryGetValue(action, out var readOnlyResult))
                {
                    LogActionResult(action, readOnlyResult);
                    return readOnlyResult;
                }
            }
            else
            {
                if (_currentState.Actions.TryGetValue(action, out var result))
                {
                    _currentState = result.resultingState;
                    LogActionResult(action, result.actionResult);
                    return result.actionResult;
                }
            }

            SyncToCurrentState();

            var actionResult = Run(action);

            if (action.IsReadOnly)
            {
                if (!TrustIsReadOnly)
                {
                    VMControl.ApplySnapshotAsync(_currentState.SnapshotId).Wait();
                }

                _currentState.ReadOnlyActions[action] = actionResult;

                return actionResult;
            }

            string actionDescription = action.GetDescription();

            string newSnapshotId = VMControl.CreateSnapshotAsync(actionDescription).Result;

            var resultingState = new VMStateTree
            {
                SnapshotId = newSnapshotId,
                SnapshotName = actionDescription,
            };

            _currentState.Actions[action] = (actionResult, resultingState);
            _currentState = resultingState;
            _currentAppliedState = resultingState;

            return actionResult;
        }

        //  Runs a command, with no state management
        VMActionResult Run(SerializedVMAction action)
        {
            if (action.Type == VMActionType.RunCommand)
            {
                var result = VMControl.RunCommandOnVM(action.Arguments.ToArray(), workingDirectory: action.WorkingDirectory);
                return new VMActionResult
                {
                    Filename = action.Arguments[0],
                    Arguments = action.Arguments.Skip(1).ToList(),
                    ExitCode = result.ExitCode,
                    StdOut = result.StdOut,
                    StdErr = result.StdErr,
                };
            }
            else if (action.Type == VMActionType.CopyFileToVM)
            {
                throw new NotImplementedException();
            }
            else if (action.Type == VMActionType.CopyFolderToVM)
            {
                var targetSharePath = VMPathToSharePath(action.TargetPath);

                var result = new RunExeCommand(Log, "robocopy", action.SourcePath, targetSharePath, "/mir")
                    .Execute()
                    .ExitCode.Should().BeLessThan(8);   //  Robocopy error exit codes are 8 or higher

                return VMActionResult.Success();
            }
            else if (action.Type == VMActionType.MoveFolderOnVM)
            {
                var sourceSharePath = VMPathToSharePath(action.SourcePath);
                var targetSharePath = VMPathToSharePath(action.TargetPath);
                Directory.Move(sourceSharePath, targetSharePath);

                return VMActionResult.Success();
            }
            else if (action.Type == VMActionType.WriteFileToVM)
            {
                var targetSharePath = VMPathToSharePath(action.TargetPath);
                if (!Directory.Exists(Path.GetDirectoryName(targetSharePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetSharePath));
                }

                File.WriteAllText(targetSharePath, action.FileContents);

                return VMActionResult.Success();
            }
            else if (action.Type == VMActionType.GetRemoteDirectory)
            {
                var targetSharePath = VMPathToSharePath(action.TargetPath);
                var result = VMActionResult.Success();

                if (Directory.Exists(targetSharePath))
                {
                    result.Exists = true;
                    result.Directories = Directory.GetDirectories(targetSharePath).Select(SharePathToVMPath).ToList();
                    result.Files = Directory.GetFiles(targetSharePath).Select(SharePathToVMPath).ToList();
                }
                else
                {
                    result.Exists = false;
                    if (action.MustExist)
                    {
                        //  Sometimes a directory that exists on the VM seems not to be found, this lets us avoid caching a bad result
                        throw new DirectoryNotFoundException($"Expected to find directory {action.TargetPath} on VM, but it was not found.");
                    }
                }
                return result;
            }
            else if (action.Type == VMActionType.GetRemoteFile)
            {
                var targetSharePath = VMPathToSharePath(action.TargetPath);
                var result = VMActionResult.Success();
                if (File.Exists(targetSharePath))
                {
                    result.Exists = true;
                    result.StdOut = File.ReadAllText(targetSharePath);
                }
                else
                {
                    result.Exists = false;
                    if (action.MustExist)
                    {
                        //  Sometimes a file that exists on the VM seems not to be found, this lets us avoid caching a bad result
                        throw new DirectoryNotFoundException($"Expected to find directory {action.TargetPath} on VM, but it was not found.");
                    }
                }
                return result;
            }
            else if (action.Type == VMActionType.ActionGroup)
            {
                List<VMActionResult> results = new();

                foreach (var subAction in action.Actions)
                {
                    results.Add(Run(subAction));
                }
                var result = VMActionResult.Success();
                if (results.Any())
                {
                    result.ExitCode = results.Last().ExitCode;
                    result.StdOut = results.Last().StdOut;
                    result.StdErr = results.Last().StdErr;
                }
                result.GroupedResults = results;

                return result;
            }
            else
            {
                throw new NotImplementedException(action.Type.ToString());
            }
        }

        public static string GetFileContentId(string path)
        {
            var info = new FileInfo(path);
            return $"{info.LastWriteTimeUtc.Ticks}-{info.Length}";
        }

        public static string GetDirectoryContentId(string path)
        {
            StringBuilder sb = new StringBuilder();
            var info = new DirectoryInfo(path);

            void ProcessDirectory(DirectoryInfo dir, string relativeTo)
            {
                foreach (var file in dir.GetFiles())
                {
                    sb.AppendLine($"{Path.GetRelativePath(relativeTo, file.FullName)}:{file.LastWriteTimeUtc.Ticks}-{file.Length}");
                }

                foreach (var subDir in dir.GetDirectories())
                {
                    sb.AppendLine(subDir.FullName);
                    ProcessDirectory(subDir, relativeTo);
                }
            }

            ProcessDirectory(info, path);

            return sb.ToString();
        }

        string VMPathToSharePath(string vmPath)
        {
            var dirInfo = new DirectoryInfo(vmPath);

            if (dirInfo.Root.FullName.Substring(1) != @":\")
            {
                throw new ArgumentException("Unrecognized directory root for: " + vmPath, nameof(vmPath));
            }

            string driveLetter = dirInfo.Root.FullName.Substring(0, 1);

            string pathUnderDrive = dirInfo.FullName.Substring(3);

            return $@"\\{VMControl.VMMachineName}\{driveLetter}$\{pathUnderDrive}";
        }

        string SharePathToVMPath(string sharePath)
        {
            if (!sharePath.StartsWith($@"\\{VMControl.VMMachineName}\"))
            {
                throw new ArgumentException("Unrecognized share path: " + sharePath, nameof(sharePath));
            }

            string pathUnderDrive = sharePath.Substring($@"\\{VMControl.VMMachineName}\".Length);

            string driveLetter = pathUnderDrive.Substring(0, 1);

            string pathAfterDrive = pathUnderDrive.Substring(3);

            return $"{driveLetter}:\\{pathAfterDrive}";
        }

        class VMRemoteFile : RemoteFile
        {
            VirtualMachine _vm;
            bool _mustExist;
            public VMRemoteFile(VirtualMachine vm, string path, bool mustExist) : base(path)
            {
                _vm = vm;
                _mustExist = mustExist;
            }

            VMActionResult GetResult()
            {
                return _vm.Apply(new SerializedVMAction()
                {
                    Type = VMActionType.GetRemoteFile,
                    TargetPath = Path,
                    IsReadOnly = true,
                    MustExist = _mustExist
                });
            }

            public override bool Exists => GetResult().Exists;

            public override string ReadAllText()
            {
                var result = GetResult();
                if (!result.Exists)
                {
                    throw new FileNotFoundException("File not found: " + Path);
                }
                return result.StdOut;
            }
        }

        class VMRemoteDirectory : RemoteDirectory
        {
            VirtualMachine _vm;
            bool _mustExist;

            public VMRemoteDirectory(VirtualMachine vm, string path, bool mustExist) : base(path)
            {
                _vm = vm;
                _mustExist = mustExist;
            }

            VMActionResult GetResult()
            {
                return _vm.Apply(new SerializedVMAction()
                {
                    Type = VMActionType.GetRemoteDirectory,
                    TargetPath = Path,
                    IsReadOnly = true,
                    MustExist = _mustExist
                });
            }

            public override bool Exists => GetResult().Exists;

            public override List<string> Directories => GetResult().Directories;

            public override List<string> Files => GetResult().Files;
        }

        public class VMSnapshot
        {
            VirtualMachine _vm;
            VMStateTree _snapshot;

            public VMSnapshot(VirtualMachine vm, VMStateTree snapshot)
            {
                _vm = vm;
                _snapshot = snapshot;
            }

            public void Apply()
            {
                _vm._currentState = _snapshot;
            }
        }
    }
}
