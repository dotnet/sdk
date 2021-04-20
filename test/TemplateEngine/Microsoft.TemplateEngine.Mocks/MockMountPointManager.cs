// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.TemplateEngine.Mocks
{
    /// <summary>
    /// Mock for IMountPointManager interface to be used for unit testing
    /// Can be set up to return failure on demanding certain mount points. The unavailable mountpoints to be defined in <see cref="UnavailableMountPoints"/>. 
    /// </summary>
    public class MockMountPointManager : IMountPointManager
    {
        public MockMountPointManager(IEngineEnvironmentSettings environmentSettings)
        {
            EnvironmentSettings = environmentSettings;
        }
        public IEngineEnvironmentSettings EnvironmentSettings
        {
            private set; get;
        }

        /// <summary>
        /// Contains the list of mount points that are considered to be unavailable when demanding.
        /// </summary>
        public List<string> UnavailableMountPoints { get; } = new List<string>();

        public void ReleaseMountPoint(IMountPoint mountPoint)
        {
            // do nothing
        }

        public bool TryDemandMountPoint(string mountPointUri, out IMountPoint mountPoint)
        {
            if (UnavailableMountPoints.Any(m => m == mountPointUri))
            {
                mountPoint = null;
                return false;
            }
            mountPoint = new MockMountPoint(EnvironmentSettings);
            return true;
        }
    }
}
