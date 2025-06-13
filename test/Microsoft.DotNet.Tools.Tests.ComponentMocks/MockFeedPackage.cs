// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Tools.Tests.ComponentMocks
{
    public class MockFeedPackage
    {
        public string PackageId;
        public string Version;
        public string ToolCommandName;
        public string ToolFormatVersion = "1";

        /// <summary>
        /// Key: Path inside package.  Value: Contents of file
        /// </summary>
        public Dictionary<string, string> AdditionalFiles { get; } = new();
    }
}
