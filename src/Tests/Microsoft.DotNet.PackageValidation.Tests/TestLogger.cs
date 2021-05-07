// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    internal class TestLogger : Logger
    {
        public List<string> errors = new();

        protected override void LogCore(in Message message) 
        {
            errors.Add(message.Text);
        }
    }
}
