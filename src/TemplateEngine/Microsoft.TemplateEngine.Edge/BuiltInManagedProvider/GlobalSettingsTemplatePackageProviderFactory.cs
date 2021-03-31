// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Edge.BuiltInManagedProvider
{
    internal class GlobalSettingsTemplatePackageProviderFactory : ITemplatePackageProviderFactory
    {
        public static readonly Guid FactoryId = new Guid("{3AACE22E-E978-4BAF-8BC1-568B290A238C}");

        public Guid Id => FactoryId;

        public string DisplayName => "Global Settings";

        public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
        {
            return new GlobalSettingsTemplatePackageProvider(this, settings);
        }
    }
}
