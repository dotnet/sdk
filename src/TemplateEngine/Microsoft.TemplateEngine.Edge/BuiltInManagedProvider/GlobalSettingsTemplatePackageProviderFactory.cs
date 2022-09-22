// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Edge.BuiltInManagedProvider
{
    public sealed class GlobalSettingsTemplatePackageProviderFactory : ITemplatePackageProviderFactory, IPrioritizedComponent
    {
        internal static readonly Guid FactoryId = new("{3AACE22E-E978-4BAF-8BC1-568B290A238C}");

        Guid IIdentifiedComponent.Id => FactoryId;

        string ITemplatePackageProviderFactory.DisplayName => "Global Settings";

        /// <summary>
        /// We want to have higher priority than SDK/OptionalWorkload providers.
        /// So user installed templates(from this provider) override those.
        /// </summary>
        int IPrioritizedComponent.Priority => 1000;

        ITemplatePackageProvider ITemplatePackageProviderFactory.CreateProvider(IEngineEnvironmentSettings settings)
        {
            return new GlobalSettingsTemplatePackageProvider(this, settings);
        }
    }
}
