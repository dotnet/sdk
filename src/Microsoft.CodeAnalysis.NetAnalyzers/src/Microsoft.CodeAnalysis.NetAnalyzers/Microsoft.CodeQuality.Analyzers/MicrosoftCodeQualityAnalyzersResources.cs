// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
