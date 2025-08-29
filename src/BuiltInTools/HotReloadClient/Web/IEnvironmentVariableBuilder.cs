// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.HotReload;

internal interface IEnvironmentVariableBuilder
{
    void SetValue(string name, string value);
    void AppendValue(string name, string value);
}
