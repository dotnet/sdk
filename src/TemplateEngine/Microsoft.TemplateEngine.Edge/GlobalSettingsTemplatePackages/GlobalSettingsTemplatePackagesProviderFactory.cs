// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackages;

namespace Microsoft.TemplateEngine.Edge
{
    public class GlobalSettingsTemplatePackagesProviderFactory : ITemplatePackagesProviderFactory
    {
        public static readonly Guid FactoryId = new Guid("{3AACE22E-E978-4BAF-8BC1-568B290A238C}");

        public Guid Id => FactoryId;

        public string Name => "Global Settings";

        public ITemplatePackagesProvider CreateProvider(IEngineEnvironmentSettings settings)
        {
            return new GlobalSettingsTemplatePackagesProvider(this, settings);
        }
    }
}
