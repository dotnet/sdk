// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

    enum VMActionType
    {
        RunCommand,
        CopyFileToVM,
        WriteFileToVM,
    }

    //  Use a single class for all actions to make it easier to serialize
    class SerializedVMAction
    {
        public VMActionType Type { get; set; }

        //  Applies to RunCommand
        public List<string> Arguments { get; set; }

        //  Applies to CopyFileToVM, WriteFileToVM
        public string TargetPath { get; set; }

        //  Applies to CopyFileToVM
        public string SourcePath { get; set; }

        //  Applies to CopyFileToVM
        public string ContentHash { get; set; }

        //  Applies to WriteFileToVM
        public string FileContents { get; set; }

        public string GetDescription()
        {
            switch (Type)
            {
                case VMActionType.RunCommand:
                    return $"Run command: {string.Join(" ", Arguments)}";
                case VMActionType.CopyFileToVM:
                    return $"Copy file to VM: {SourcePath} -> {TargetPath}";
                case VMActionType.WriteFileToVM:
                    return $"Write file to VM: {TargetPath}";
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

            if ((Arguments == null) != (action.Arguments == null))
            {
                return false;
            }

            if (Arguments != null && !Arguments.SequenceEqual(action.Arguments))
            {
                return false;
            }

            return Type == action.Type &&
                   TargetPath == action.TargetPath &&
                   SourcePath == action.SourcePath &&
                   ContentHash == action.ContentHash &&
                   FileContents == action.FileContents;
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
            hashcode.Add(ContentHash);
            hashcode.Add(FileContents);
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
    }
}
