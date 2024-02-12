// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Abstractions.Parameters;

namespace Microsoft.TemplateEngine.Utils
{
    public static class IScanTemplateInfoExtensions
    {
        /// <summary>
        /// Converts <see cref="IScanTemplateInfo"/> to <see cref="ITemplateInfo"/>.
        /// </summary>
        /// <param name="templateInfo"><see cref="IScanTemplateInfo"/> to convert.</param>
        /// <param name="locFilePath">the path to localization file to use in <see cref="ITemplateInfo"/>.</param>
        /// <param name="hostFilePath">the path to host config file to use in <see cref="ITemplateInfo"/>.</param>
        public static ITemplateInfo ToITemplateInfo(this IScanTemplateInfo templateInfo, string? locFilePath = null, string? hostFilePath = null) => new LegacyTemplateInfo(templateInfo, locFilePath, hostFilePath);

        private class LegacyTemplateInfo : ITemplateInfo
        {
            private readonly IScanTemplateInfo _templateInfo;
            private readonly string? _locFilePath;
            private readonly string? _hostFilePath;

            internal LegacyTemplateInfo(IScanTemplateInfo templateInfo, string? locFilePath = null, string? hostFilePath = null)
            {
                _templateInfo = templateInfo;
                _locFilePath = locFilePath;
                _hostFilePath = hostFilePath;
            }

            public string? Author => _templateInfo.Author;

            public string? Description => _templateInfo.Description;

            public IReadOnlyList<string> Classifications => _templateInfo.Classifications;

            public string? DefaultName => _templateInfo.DefaultName;

            public string Identity => _templateInfo.Identity;

            public Guid GeneratorId => _templateInfo.GeneratorId;

            public string? GroupIdentity => _templateInfo.GroupIdentity;

            public int Precedence => _templateInfo.Precedence;

            public string Name => _templateInfo.Name;

            [Obsolete]
#pragma warning disable CA1826 // Do not use Enumerable methods on indexable collections
            public string ShortName => _templateInfo.ShortNameList.FirstOrDefault() ?? string.Empty;
#pragma warning restore CA1826 // Do not use Enumerable methods on indexable collections

            [Obsolete]
            public IReadOnlyDictionary<string, ICacheTag> Tags => throw new NotSupportedException();

            public IReadOnlyDictionary<string, string> TagsCollection => _templateInfo.TagsCollection;

            [Obsolete]
            public IReadOnlyDictionary<string, ICacheParameter> CacheParameters => throw new NotSupportedException();

            public IParameterDefinitionSet ParameterDefinitions => _templateInfo.ParameterDefinitions;

            [Obsolete]
            public IReadOnlyList<ITemplateParameter> Parameters => _templateInfo.ParameterDefinitions;

            public string MountPointUri => _templateInfo.MountPointUri;

            public string ConfigPlace => _templateInfo.ConfigPlace;

            public string? LocaleConfigPlace => _locFilePath;

            public string? HostConfigPlace => _hostFilePath;

            public string? ThirdPartyNotices => _templateInfo.ThirdPartyNotices;

            public IReadOnlyDictionary<string, IBaselineInfo> BaselineInfo => _templateInfo.BaselineInfo;

            [Obsolete]
            public bool HasScriptRunningPostActions { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public IReadOnlyList<string> ShortNameList => _templateInfo.ShortNameList;

            public IReadOnlyList<Guid> PostActions => _templateInfo.PostActions;

            public IReadOnlyList<TemplateConstraintInfo> Constraints => _templateInfo.Constraints;

            public bool PreferDefaultName => _templateInfo.PreferDefaultName;
        }
    }
}
