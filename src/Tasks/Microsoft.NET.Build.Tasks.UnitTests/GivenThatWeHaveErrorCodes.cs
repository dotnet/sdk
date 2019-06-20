﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;
using System.Reflection.Metadata;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenThatWeHaveErrorCodes
    {
        private const int _firstCode = 1001;

        private static readonly IReadOnlyList<int> _deletedCodes = new int[]
        {
            1026,
            1027,
            1033,
            1034,
            1035,
            1036,
            1037,
            1038,
            1039,
            1040,
            1041,
            1057,
            1062,
            1066,
            1101,
        };

        [Fact]
        public void ThereAreNoGapsDuplicatesOrIncorrectlyFormattedCodes()
        {
            var codes = new HashSet<int>(_deletedCodes);

            foreach (var (key, message) in GetMessages())
            {
                var match = Regex.Match(message, "^NETSDK([0-9]{4}): ");

                if (key.EndsWith("_Info"))
                {
                    match.Success
                        .Should()
                        .BeFalse(because: "informational messages should not have error codes.");
                }
                else
                {
                    match.Success
                        .Should()
                        .BeTrue(because: $"all non-informational should have correctly formatted error codes ({key} does not).");

                    int code = int.Parse(match.Groups[1].Value);
                    codes.Add(code)
                        .Should()
                        .BeTrue(because: $"error codes should not be duplicated (NETSDK{code} is used more than once)");
                }
            }

            for (int i = 0; i < codes.Count; i++)
            {
                int code = _firstCode + i;
                codes.Contains(code)
                     .Should()
                     .BeTrue(because: $"error codes should not be skipped (NETSDK{code} was not found; add to the deleted codes list if intentionally deleted)");
            }
        }

        [Fact]
        public void ResxIsCommentedWithCorrectStrBegin()
        {
            var doc = XDocument.Load("Strings.resx");
            var ns = doc.Root.Name.Namespace;

            foreach (var data in doc.Root.Elements(ns + "data"))
            {
                var name = data.Attribute("name").Value;
                var value = data.Element(ns + "value").Value;
                var comment = data.Element(ns + "comment")?.Value ?? "";
                var prefix = value.Substring(0, value.IndexOf(' '));
                
                if (name.EndsWith("_Info"))
                { 
                    comment.Should().NotContain("StrBegin",
                        because: "informational messages should not have error codes.");
                }
                else
                {
                    comment.Should().StartWith($@"{{StrBegin=""{prefix} ""}}",
                        because: "localization instructions should indicate invariant error code as preceding translatable message.");
                }
            }
        }

        private static IEnumerable<(string key, string message)> GetMessages()
        {
            var set = Strings.ResourceManager.GetResourceSet(
                CultureInfo.InvariantCulture,
                createIfNotExists: true,
                tryParents: false);

            return set.Cast<DictionaryEntry>().Select(e => ((string)e.Key, (string)e.Value));
        }
    }
}
