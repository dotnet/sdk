// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

internal sealed class InvalidAuthRealmConfigurationException : ArgumentException
{
    public InvalidAuthRealmConfigurationException(string resourceName, string environmentVariableName, string invalidValue)
        : base(Resource.FormatString(resourceName, environmentVariableName, invalidValue))
    {
        ResourceName = resourceName;
        EnvironmentVariableName = environmentVariableName;
        InvalidValue = invalidValue;
    }

    public string ResourceName { get; }

    public string EnvironmentVariableName { get; }

    public string InvalidValue { get; }
}
