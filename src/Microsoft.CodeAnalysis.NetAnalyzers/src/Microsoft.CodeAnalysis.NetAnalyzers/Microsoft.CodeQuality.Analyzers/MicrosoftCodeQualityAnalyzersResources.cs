// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.CodeAnalysis;

namespace Microsoft.CodeQuality.Analyzers
{
    internal partial class MicrosoftCodeQualityAnalyzersResources
    {
        private static readonly Type s_resourcesType = typeof(MicrosoftCodeQualityAnalyzersResources);

        public static LocalizableResourceString CreateLocalizableResourceString(string nameOfLocalizableResource)
            => new(nameOfLocalizableResource, ResourceManager, s_resourcesType);

        public static LocalizableResourceString CreateLocalizableResourceString(string nameOfLocalizableResource, params string[] formatArguments)
            => new(nameOfLocalizableResource, ResourceManager, s_resourcesType, formatArguments);
    }
}
