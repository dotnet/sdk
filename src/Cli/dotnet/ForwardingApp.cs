﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.DotNet.Cli
{
    public class ForwardingApp
    {
        private ForwardingAppImplementation _implementation;

        public ForwardingApp(
            string forwardApplicationPath,
            IEnumerable<string> argsToForward,
            string depsFile = null,
            string runtimeConfig = null,
            string additionalProbingPath = null,
            Dictionary<string, string> environmentVariables = null)
        {
            _implementation = new ForwardingAppImplementation(
                forwardApplicationPath,
                argsToForward,
                depsFile,
                runtimeConfig,
                additionalProbingPath,
                environmentVariables);
        }

        public ProcessStartInfo GetProcessStartInfo()
        {
            return _implementation.GetProcessStartInfo();
        }

        public ForwardingApp WithEnvironmentVariable(string name, string value)
        {
            _implementation = _implementation.WithEnvironmentVariable(name, value);
            return this;
        }

        public int Execute()
        {
            return _implementation.Execute();
        }
    }
}
