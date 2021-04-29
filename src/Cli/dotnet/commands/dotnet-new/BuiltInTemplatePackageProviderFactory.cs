﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.DotNet.Tools.New
{
    /// <summary>
    /// Factory that is loaded by TemplateEngine via <see cref="IComponentManager"/>.
    /// To create <see cref="ITemplatePackageProvider"/> which returns list of packages to be installed.
    /// </summary>
    internal class BuiltInTemplatePackageProviderFactory : ITemplatePackageProviderFactory
    {
        public static readonly Guid FactoryId = new Guid("{4B11226E-4594-43A4-B843-EB97447B6455}");

        public string DisplayName => "BuiltIn SDK packages";

        public Guid Id { get => FactoryId; }

        public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
            => new BuiltInTemplatePackageProvider(this, settings);
    }
}
