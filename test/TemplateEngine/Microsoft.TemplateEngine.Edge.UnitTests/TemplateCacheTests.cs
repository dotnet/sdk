// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

#if !NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
#endif
using System.Globalization;
using FakeItEasy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge.Settings;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class TemplateCacheTests
    {
        [Theory]
        [InlineData("en-US", "en")]
        [InlineData("zh-CN", "zh-Hans")]
        [InlineData("zh-SG", "zh-Hans")]
        [InlineData("zh-TW", "zh-Hant")]
        [InlineData("zh-HK", "zh-Hant")]
        [InlineData("zh-MO", "zh-Hant")]
        [InlineData("pt-BR", "pt-BR")]
        [InlineData("pt", null)]
        [InlineData("uk-UA", null)]
        [InlineData("invariant", null)]
        public void PicksCorrectLocator (string currentCulture, string? expectedLocator)
        {
            CultureInfo persistedCulture = CultureInfo.CurrentUICulture;
            try
            {
                if (currentCulture != "invariant")
                {
                    CultureInfo.CurrentUICulture = new CultureInfo(currentCulture);
                }
                else
                {
                    currentCulture = "";
                    CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
                }
                string[] availableLocales = new[] { "cs", "de", "en", "es", "fr", "it", "ja", "ko", "pl", "pt-BR", "ru", "tr", "zh-Hans", "zh-Hant" };

                ITemplate template = A.Fake<ITemplate>();
                A.CallTo(() => template.Identity).Returns("testIdentity");
                List<ILocalizationLocator> locators = new List<ILocalizationLocator>();
                foreach (string locale in availableLocales)
                {
                    ILocalizationLocator locator = A.Fake<ILocalizationLocator>();
                    A.CallTo(() => locator.Identity).Returns("testIdentity");
                    A.CallTo(() => locator.Locale).Returns(locale);
                    A.CallTo(() => locator.Name).Returns(locale + " name");
                    locators.Add(locator);
                }
                IMountPoint mountPoint = A.Fake<IMountPoint>();
                A.CallTo(() => mountPoint.MountPointUri).Returns("testMount");

                ScanResult result = new ScanResult(mountPoint, new[] { template }, locators, Array.Empty<(string AssemblyPath, Type InterfaceType, IIdentifiedComponent Instance)>());

                TemplateCache templateCache = new TemplateCache(new[] { result }, new Dictionary<string, DateTime>(), NullLogger.Instance);

                Assert.Equal(currentCulture, templateCache.Locale);
                Assert.Equal("testIdentity", templateCache.TemplateInfo.Single().Identity);
                Assert.Equal(string.IsNullOrEmpty(expectedLocator) ? "" : expectedLocator + " name", templateCache.TemplateInfo.Single().Name);
            }
            finally
            {
                CultureInfo.CurrentUICulture = persistedCulture;
            }
        }

    }
}
