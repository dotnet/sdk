// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IEngineEnvironmentSettings
    {
        ISettingsLoader SettingsLoader { get; }

        ITemplateEngineHost Host { get; }

        IEnvironment Environment { get; }

        IPathInfo Paths { get; }
    }
}