// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier
{
    [Flags]
    public enum UniqueForOption
    {
        None = 0,
        Architecture = 1,
        OsPlatform = 2,
        Runtime = 4,
        RuntimeAndVersion = 8,
        TargetFramework = 16,
        TargetFrameworkAndVersion = 32
    }
}
