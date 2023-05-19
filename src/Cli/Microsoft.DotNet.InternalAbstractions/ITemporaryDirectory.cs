﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.EnvironmentAbstractions
{
    internal interface ITemporaryDirectory : IDisposable
    {
        string DirectoryPath { get; }
    }
}
