// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

        private static DateTime defaultUtcTime = new DateTime(1601, 1, 1, 0, 0, 0);

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

        public AndWhichConstraint<FileInfoAssertions, DateTimeOffset> HaveLastWriteTimeUtc(string because = "", params object[] reasonArgs)
        {
            var lastWriteTimeUtc = _fileInfo.LastWriteTimeUtc;

            // If last write time is not valid, it will be defaultUtcTime
            Execute.Assertion
                .ForCondition(!lastWriteTimeUtc.Equals(defaultUtcTime))
                .BecauseOf(because, reasonArgs) 
                .FailWith($"Expected File {_fileInfo.FullName} to have a LastWriteTimeUTC, but it is null.");
            return new AndWhichConstraint<FileInfoAssertions, DateTimeOffset>(this, lastWriteTimeUtc);
        }
    }
}
