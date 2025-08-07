// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.MsiInstallerTests.Framework
{
    abstract class VMAction
    {
        public VirtualMachine VM { get; }

        protected VMAction(VirtualMachine vm)
        {
            VM = vm;
        }

        public string ExplicitDescription { get; set; }

        //  Indicates that the action doesn't change the state of the VM, which allows extra snapshots to be avoided
        public bool IsReadOnly { get; set; }

        public CommandResult Execute()
        {
            return VM.Apply(Serialize()).ToCommandResult();
        }

        public VMAction WithDescription(string description)
        {
            ExplicitDescription = description;
            return this;
        }

        public VMAction WithIsReadOnly(bool isReadOnly)
        {
            IsReadOnly = isReadOnly;
            return this;
        }

        public SerializedVMAction Serialize()
        {
            var serialized = SerializeDerivedProperties();
            serialized.ExplicitDescription = ExplicitDescription;
            serialized.IsReadOnly = IsReadOnly;
            return serialized;
        }
        protected abstract SerializedVMAction SerializeDerivedProperties();
    }

    class VMRunAction : VMAction
    {
        public List<string> Arguments { get; set; }

        public string WorkingDirectory { get; set; }

        public VMRunAction(VirtualMachine vm, List<string> arguments = null) : base(vm)
        {
            Arguments = arguments ?? new List<string>();
        }

        public VMRunAction WithWorkingDirectory(string workingDirectory)
        {
            WorkingDirectory = workingDirectory;
            return this;
        }

        protected override SerializedVMAction SerializeDerivedProperties()
        {
            return new SerializedVMAction
            {
                Type = VMActionType.RunCommand,
                Arguments = Arguments,
                WorkingDirectory = WorkingDirectory,
            };
        }
    }

    class VMCopyFileAction : VMAction
    {
        public string LocalSource { get; set; }
        public string TargetPath { get; set; }

        public VMCopyFileAction(VirtualMachine vm) : base(vm)
        {
        }

        protected override SerializedVMAction SerializeDerivedProperties()
        {
            return new SerializedVMAction
            {
                Type = VMActionType.CopyFileToVM,
                SourcePath = LocalSource,
                TargetPath = TargetPath,
                ContentId = VirtualMachine.GetFileContentId(LocalSource),

            };
        }
    }

    class VMCopyFolderAction : VMAction
    {
        public string LocalSource { get; set; }
        public string TargetPath { get; set; }

        public VMCopyFolderAction(VirtualMachine vm) : base(vm)
        {
        }

        protected override SerializedVMAction SerializeDerivedProperties()
        {
            return new SerializedVMAction
            {
                Type = VMActionType.CopyFolderToVM,
                SourcePath = LocalSource,
                TargetPath = TargetPath,
                ContentId = VirtualMachine.GetDirectoryContentId(LocalSource),
            };
        }
    }

    class VMMoveFolderAction : VMAction
    {
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }

        public VMMoveFolderAction(VirtualMachine vm) : base(vm)
        {
        }

        protected override SerializedVMAction SerializeDerivedProperties()
        {
            return new SerializedVMAction
            {
                Type = VMActionType.MoveFolderOnVM,
                SourcePath = SourcePath,
                TargetPath = TargetPath,
            };
        }
    }

    class VMWriteFileAction : VMAction
    {
        public string TargetPath { get; set; }
        public string FileContents { get; set; }

        public VMWriteFileAction(VirtualMachine vm) : base(vm)
        {
        }

        protected override SerializedVMAction SerializeDerivedProperties()
        {
            return new SerializedVMAction
            {
                Type = VMActionType.WriteFileToVM,
                TargetPath = TargetPath,
                FileContents = FileContents,
            };
        }
    }

    class VMGroupedAction : VMAction
    {
        public List<VMAction> Actions { get; set; }

        public VMGroupedAction(VirtualMachine vm) : base(vm)
        {
            Actions = new List<VMAction>();
        }

        protected override SerializedVMAction SerializeDerivedProperties()
        {
            return new SerializedVMAction
            {
                Type = VMActionType.ActionGroup,
                Actions = Actions.Select(a => a.Serialize()).ToList(),
            };
        }
    }

    enum VMActionType
    {
        RunCommand,
        CopyFileToVM,
        CopyFolderToVM,
        MoveFolderOnVM,
        WriteFileToVM,
        GetRemoteDirectory,
        GetRemoteFile,
        //  Used to combine multiple actions into a single action, so they don't get snapshotted separately
        ActionGroup,
    }

    //  Use a single class for all actions to make it easier to serialize
    class SerializedVMAction
    {
        public VMActionType Type { get; set; }

        public string ExplicitDescription { get; set; }

        public bool IsReadOnly { get; set; }

        //  Applies to RunCommand
        public List<string> Arguments { get; set; }

        //  Applies to RunCommand
        public string WorkingDirectory { get; set; }

        //  Applies to CopyFileToVM, CopyFolderToVM, MoveFolderOnVM, WriteFileToVM, GetRemoteDirectory, GetRemoteFile
        public string TargetPath { get; set; }

        //  Applies to GetRemoteDirectory, GetRemoteFile
        public bool MustExist { get; set; }

        //  Applies to CopyFileToVM, CopyFolderToVM, MoveFolderOnVM
        public string SourcePath { get; set; }

        //  Applies to CopyFileToVM, CopyFolderToVM
        /// <summary>
        /// Identifier for the contents of the file or folder to copy.  This could be a hash, but for our purposes a combination of the file size and last modified time should work.
        /// </summary>
        public string ContentId { get; set; }

        //  Applies to WriteFileToVM
        public string FileContents { get; set; }

        //  Applies to ActionGroup
        public List<SerializedVMAction> Actions { get; set; }

        public string GetDescription()
        {
            if (!string.IsNullOrEmpty(ExplicitDescription))
            {
                return ExplicitDescription;
            }
            switch (Type)
            {
                case VMActionType.RunCommand:
                    return $"Run: {string.Join(" ", Arguments)}";
                case VMActionType.CopyFileToVM:
                    return $"Copy file to VM: {SourcePath} -> {TargetPath}";
                case VMActionType.CopyFolderToVM:
                    return $"Copy folder to VM: {SourcePath} -> {TargetPath}";
                case VMActionType.MoveFolderOnVM:
                    return $"Move folder {SourcePath} -> {TargetPath}";
                case VMActionType.WriteFileToVM:
                    return $"Write file to VM: {TargetPath}";
                case VMActionType.GetRemoteDirectory:
                    return $"Get remote directory: {TargetPath}";
                case VMActionType.GetRemoteFile:
                    return $"Get remote file: {TargetPath}";
                case VMActionType.ActionGroup:
                    return $"Action group: {string.Join(", ", Actions.Select(a => a.GetDescription()))}";
                default:
                    throw new NotImplementedException();
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is not SerializedVMAction action)
            {
                return false;
            }

            static bool ListsAreEqual<T>(List<T> a, List<T> b)
            {
                if (a == null && b == null)
                {
                    return true;
                }
                if (a == null || b == null)
                {
                    return false;
                }
                return a.SequenceEqual(b);
            }

            return Type == action.Type &&
                   ExplicitDescription == action.ExplicitDescription &&
                   IsReadOnly == action.IsReadOnly &&
                   ListsAreEqual(Arguments, action.Arguments) &&
                   WorkingDirectory == action.WorkingDirectory &&
                   TargetPath == action.TargetPath &&
                   SourcePath == action.SourcePath &&
                   ContentId == action.ContentId &&
                   FileContents == action.FileContents &&
                   ListsAreEqual(Actions, action.Actions);
        }

        public override int GetHashCode()
        {
            var hashcode = new HashCode();
            hashcode.Add(Type);
            hashcode.Add(ExplicitDescription);
            hashcode.Add(IsReadOnly);
            if (Arguments != null)
            {
                hashcode.Add(Arguments.Count);
                foreach (var arg in Arguments)
                {
                    hashcode.Add(arg.GetHashCode());
                }
            }
            hashcode.Add(WorkingDirectory);
            hashcode.Add(TargetPath);
            hashcode.Add(SourcePath);
            hashcode.Add(ContentId);
            hashcode.Add(FileContents);
            if (Actions != null)
            {
                hashcode.Add(Actions.Count);
                foreach (var action in Actions)
                {
                    hashcode.Add(action.GetHashCode());
                }
            }
            return hashcode.ToHashCode();
        }

        public static bool operator ==(SerializedVMAction left, SerializedVMAction right) => EqualityComparer<SerializedVMAction>.Default.Equals(left, right);
        public static bool operator !=(SerializedVMAction left, SerializedVMAction right) => !(left == right);
    }

    class VMActionResult
    {
        public string Filename { get; set; }
        public List<string> Arguments { get; set; }
        public int ExitCode { get; set; }
        public string StdOut { get; set; }
        public string StdErr { get; set; }

        //  For GetRemoteDirectory and GetRemoteFile
        public bool Exists { get; set; }
        public List<string> Directories { get; set; }
        public List<string> Files { get; set; }

        public List<VMActionResult> GroupedResults { get; set; }

        public CommandResult ToCommandResult()
        {
            var psi = new ProcessStartInfo
            {
                FileName = Filename,
                //  This doesn't handle quoting arguments with spaces correctly, but we don't expect this to be used except for logging
                Arguments = Arguments == null ? null : string.Join(" ", Arguments),
            };

            return new CommandResult(
                psi,
                ExitCode,
                StdOut,
                StdErr);
        }

        public static VMActionResult Success()
        {
            return new VMActionResult
            {
                ExitCode = 0,
                StdOut = "",
                StdErr = "",
            };
        }
    }
}
