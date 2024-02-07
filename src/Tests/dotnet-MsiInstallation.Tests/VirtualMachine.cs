// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.MsiInstallerTests
{
    class VirtualMachine : IDisposable
    {
        ITestOutputHelper Log { get; }
        public VMControl VMControl { get; }

        static VMStateTree _rootState;
        VMStateTree _currentState;
        VMStateTree _currentAppliedState;

        string _stateFile;

        //  Whether we should trust the ShouldChangeState property of the action.  If not, then we will re-apply the previous snapshot after the action runs to make sure the state hasn't been polluted.
        public bool TrustShouldChangeState { get; set; } = true;

        public VirtualMachine(ITestOutputHelper log)
        {
            Log = log;
            VMControl = new VMControl(log);

            _stateFile = Path.Combine(Environment.CurrentDirectory, "vmstate.json");

            //  Load root state from file, if it exists
            if (File.Exists(_stateFile))
            {
                string json = File.ReadAllText(_stateFile);
                _rootState = JsonSerializer.Deserialize<SerializeableVMStateTree>(json, GetSerializerOptions()).ToVMStateTree();
            }
            else
            {
                if (_rootState == null)
                {
                    _rootState = new VMStateTree()
                    {
                        SnapshotId = "CF853278-1B6B-4816-8E51-FBDBE3AABA2C",
                        SnapshotName = "Set network to private",
                    };
                }
            }

            _currentState = _rootState;
        }
        public void Dispose()
        {
            string json = JsonSerializer.Serialize(_rootState.ToSerializeable(), GetSerializerOptions());
            File.WriteAllText(_stateFile, json);

            VMControl.Dispose();
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

        public RemoteFile GetRemoteFile(string path)
        {
            return new VMRemoteFile(this, path);
        }

        public RemoteDirectory GetRemoteDirectory(string path)
        {
            return new VMRemoteDirectory(this, path);
        }

        void SyncToCurrentState()
        {
            if (_currentAppliedState != _currentState)
            {
                VMControl.ApplySnapshotAsync(_currentState.SnapshotId).Wait();
                _currentAppliedState = _currentState;
            }
        }

        //  Runs a command if necessary, or returns previously recorded result.  Handles syncing to the correct state, creating a new snapshot, etc.
        public VMActionResult Apply(SerializedVMAction action)
        {
            if (_currentState.Actions.TryGetValue(action, out var result))
            {
                _currentState = result.resultingState;
                return result.actionResult;
            }

            SyncToCurrentState();

            var actionResult = Run(action);

            if (!action.ShouldChangeState)
            {
                if (!TrustShouldChangeState)
                {
                    VMControl.ApplySnapshotAsync(_currentState.SnapshotId).Wait();
                }

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
                var result = VMControl.RunCommandOnVM(action.Arguments.ToArray());
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

                CopyDirectory(action.SourcePath, targetSharePath);

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
            else if (action.Type == VMActionType.ActionGroup)
            {
                VMActionResult lastResult = VMActionResult.Success();

                foreach (var subAction in action.Actions)
                {
                    lastResult = Run(subAction);
                }

                return lastResult;
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

        static void CopyDirectory(string sourcePath, string destPath)
        {
            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }

            foreach (var dir in Directory.GetDirectories(sourcePath))
            {
                CopyDirectory(dir, Path.Combine(destPath, Path.GetFileName(dir)));
            }

            foreach (var file in Directory.GetFiles(sourcePath))
            {
                new FileInfo(file).CopyTo(Path.Combine(destPath, Path.GetFileName(file)), true);
            }
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

        class VMRemoteFile : RemoteFile
        {
            VirtualMachine _vm;
            public VMRemoteFile(VirtualMachine vm, string path) : base(path)
            {
                _vm = vm;
            }

            public override string ReadAllText()
            {
                _vm.SyncToCurrentState();
                return File.ReadAllText(_vm.VMPathToSharePath(Path));
            }
        }

        class VMRemoteDirectory : RemoteDirectory
        {
            VirtualMachine _vm;
            public VMRemoteDirectory(VirtualMachine vm, string path) : base(path)
            {
                _vm = vm;
            }

            public override bool Exists
            {
                get
                {
                    _vm.SyncToCurrentState();
                    return Directory.Exists(_vm.VMPathToSharePath(Path));
                }
            }
        }
    }
}
