// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    public class FileSkipData
    {
        public string? SourceProvider { get; set; }
        public string? SourceFilePath { get; set; }
        public string? DestinationProvider { get; set; }
        public string? DestinationFilePath { get; set; }
    }
}
