// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Extensions.EnvironmentAbstractions
{
    internal class EnvironmentWrapper : IEnvironment
    {
        public static IEnvironment Default = new EnvironmentWrapper();

        public string GetEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name);
        }
    }
}
