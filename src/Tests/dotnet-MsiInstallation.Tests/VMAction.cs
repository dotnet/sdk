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

namespace Microsoft.DotNet.MsiInstallerTests
{
    //  Core actions:
    //  - Run command
    //  - Copy file to VM
    //  - Enumerate and read files / directories
    //    - Is this actually an action?

    //  Run command
    //  - Install .NET SDK (From SdkTesting folder)
    //  - Uninstall .NET SDK
    //  - Workload install / uninstall
    //  - Apply rollback file
    //  - Get .NET version - this shouldn't change state
    //  Copy file to VM
    //  - Deploy stage 2 SDK

    abstract class VMAction
    {
        public VirtualMachine VM { get; }

        protected VMAction(VirtualMachine vm)
        {
            VM = vm;
        }

        public string ExplicitDescription { get; set; }

        public bool ShouldChangeState { get; set; } = true;

        public CommandResult Execute()
        {
            return VM.Apply(Serialize()).ToCommandResult();
        }

        public VMAction WithDescription(string description)
        {
            ExplicitDescription = description;
            return this;
        }

        public SerializedVMAction Serialize()
        {
            var serialized = SerializeDerivedProperties();
            serialized.ExplicitDescription = ExplicitDescription;
            serialized.ShouldChangeState = ShouldChangeState;
            return serialized;
        }
        protected abstract SerializedVMAction SerializeDerivedProperties();
    }

    class VMRunAction : VMAction
    {
        public List<string> Arguments { get; set; }

        public VMRunAction(VirtualMachine vm, List<string> arguments = null) : base(vm)
        {
            Arguments = arguments ?? new List<string>();
        }

        protected override SerializedVMAction SerializeDerivedProperties()
        {
            return new SerializedVMAction
            {
                Type = VMActionType.RunCommand,
                Arguments = Arguments,
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
        WriteFileToVM,
        //  Used to combine multiple actions into a single action, so they don't get snapshotted separately
        ActionGroup,
    }

    //  Use a single class for all actions to make it easier to serialize
    class SerializedVMAction
    {
        public VMActionType Type { get; set; }

        public string ExplicitDescription { get; set; }

        public bool ShouldChangeState { get; set; }

        //  Applies to RunCommand
        public List<string> Arguments { get; set; }

        //  Applies to CopyFileToVM, CopyFolderToVM, WriteFileToVM
        public string TargetPath { get; set; }

        //  Applies to CopyFileToVM, CopyFolderToVM
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
                case VMActionType.WriteFileToVM:
                    return $"Write file to VM: {TargetPath}";
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
                   ShouldChangeState == action.ShouldChangeState &&
                   ListsAreEqual(Arguments, action.Arguments) &&
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
            if (Arguments != null)
            {
                hashcode.Add(Arguments.Count);
                foreach (var arg in Arguments)
                {
                    hashcode.Add(arg.GetHashCode());
                }
            }
            hashcode.Add(TargetPath);
            hashcode.Add(SourcePath);
            hashcode.Add(ContentId);
            hashcode.Add(FileContents);
            return hashcode.ToHashCode();
        }

        public static bool operator ==(SerializedVMAction left, SerializedVMAction right) => EqualityComparer<SerializedVMAction>.Default.Equals(left, right);
        public static bool operator !=(SerializedVMAction left, SerializedVMAction right) => !(left == right);
    }

    //  Do we need a separate VMActionResult, or should we just use CommandResult?
    class VMActionResult
    {
        public string Filename { get; set; }
        public List<string> Arguments { get; set; }
        public int ExitCode { get; set; }
        public string StdOut { get; set; }
        public string StdErr { get; set; }

        public CommandResult ToCommandResult()
        {
            return new CommandResult(
                Arguments == null ? null : new ProcessStartInfo
                {
                    FileName = Arguments[0],
                    //  This doesn't handle quoting arguments with spaces correctly, but we don't expect this to be used except for logging
                    Arguments = string.Join(" ", Arguments.Skip(1)),
                },
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
