﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.NET.TestFramework.Assertions
{
    public static partial class FileInfoExtensions
    {
        private class FileInfoLock : IDisposable
        {
            private FileStream _fileStream;

            public FileInfoLock(FileInfo fileInfo)
            {
                _fileStream = fileInfo.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }

            public void Dispose()
            {
                _fileStream.Dispose();
            }
        }
    }
}
