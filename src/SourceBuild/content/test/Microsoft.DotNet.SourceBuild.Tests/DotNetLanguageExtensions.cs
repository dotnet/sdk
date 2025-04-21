// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.SourceBuild.Tests;

public static class DotNetLanguageExtensions
{
    public static string ToCliName(this DotNetLanguage language) => language switch
    {
        DotNetLanguage.CSharp => "C#",
        DotNetLanguage.FSharp => "F#",
        DotNetLanguage.VB => "VB",
        _ => throw new NotImplementedException()
    };
}
