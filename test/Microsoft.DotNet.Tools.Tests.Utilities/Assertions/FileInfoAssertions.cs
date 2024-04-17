// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using FluentAssertions.Execution;
using System.IO;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class FileInfoAssertions
    {
        private FileInfo _fileInfo;

        public FileInfoAssertions(FileInfo file)
        {
            _fileInfo = file;
        }

        public FileInfo FileInfo => _fileInfo;

        public AndConstraint<FileInfoAssertions> Exist(string because = "", params object[] reasonArgs)
        {
            Execute.Assertion
                .ForCondition(_fileInfo.Exists)
                .BecauseOf(because, reasonArgs) 
                .FailWith($"Expected File {_fileInfo.FullName} to exist, but it does not.");
            return new AndConstraint<FileInfoAssertions>(this);
        }

        public AndConstraint<FileInfoAssertions> NotExist(string because = "", params object[] reasonArgs)
        {
            Execute.Assertion
                .ForCondition(!_fileInfo.Exists)
                .BecauseOf(because, reasonArgs) 
                .FailWith($"Expected File {_fileInfo.FullName} to not exist, but it does.");
            return new AndConstraint<FileInfoAssertions>(this);
        }
    }
}
