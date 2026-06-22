// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Cli.Commands;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Cli.UnitTests.CliMocks;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateResolutionTests
{
    [TestClass]
    public class ListTemplateResolverTests : BaseTest
    {
        [TestMethod]
        public async Task TestGetTemplateResolutionResult_UniqueNameMatchesCorrectly()
        {
            IReadOnlyList<ITemplateInfo> templatesToSearch = new List<ITemplateInfo>()
            {
                new MockTemplateInfo("console1", name: "Long name for Console App", identity: "Console.App"),
                new MockTemplateInfo("console2", name: "Long name for Console App #2", identity: "Console.App2")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                GetListCommandArgsFor("new list console2"),
                defaultLanguage: null,
                TestContext.CancellationToken);

            Assert.IsTrue(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.IsNotNull(matchResult.UnambiguousTemplateGroup);
            Assert.AreEqual("console2", matchResult.UnambiguousTemplateGroup?.Templates.Single().ShortNameList.Single());
            Assert.AreEqual("Console.App2", matchResult.UnambiguousTemplateGroup?.Templates.Single().Identity);
            Assert.HasCount(1, matchResult.UnambiguousTemplateGroup?.Templates!);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_ExactMatchOnShortNameMatchesCorrectly()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App"),
                new MockTemplateInfo("console2", name: "Long name for Console App #2", identity: "Console.App2")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                GetListCommandArgsFor("new list console"),
                defaultLanguage: null,
                TestContext.CancellationToken);

            Assert.IsTrue(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.IsNull(matchResult.UnambiguousTemplateGroup);
            Assert.HasCount(2, matchResult.TemplateGroupsWithMatchingTemplateInfo);
            Assert.IsNotNull(matchResult.TemplateGroupsWithMatchingTemplateInfo.SelectMany(group => group.Templates).Single(t => t.Identity == "Console.App"));
            Assert.IsNotNull(matchResult.TemplateGroupsWithMatchingTemplateInfo.SelectMany(group => group.Templates).Single(t => t.Identity == "Console.App2"));
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_UnambiguousGroupIsFound()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"),
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"),
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L3", groupIdentity: "Console.App.Test").WithTag("language", "L3")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                GetListCommandArgsFor("new list console"),
                defaultLanguage: null,
                TestContext.CancellationToken);

            Assert.IsTrue(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.HasCount(1, matchResult.TemplateGroupsWithMatchingTemplateInfo);
            Assert.HasCount(3, matchResult.TemplateGroupsWithMatchingTemplateInfo.Single().Templates);
            Assert.IsNotNull(matchResult.UnambiguousTemplateGroup);
            Assert.HasCount(3, matchResult.UnambiguousTemplateGroup!.Templates);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_MultipleGroupsAreFound()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"),
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2"),
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L3", groupIdentity: "Console.App.Test").WithTag("language", "L3"),
                new MockTemplateInfo("classlib", name: "Long name for Class Library App", identity: "Class.Library.L1", groupIdentity: "Class.Library.Test").WithTag("language", "L1"),
                new MockTemplateInfo("classlib", name: "Long name for Class Library App", identity: "Class.Library.L2", groupIdentity: "Class.Library.Test").WithTag("language", "L2")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                GetListCommandArgsFor("new list c"),
                defaultLanguage: null,
                TestContext.CancellationToken);

            Assert.IsTrue(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.HasCount(2, matchResult.TemplateGroupsWithMatchingTemplateInfo);
            Assert.HasCount(5, matchResult.TemplateGroupsWithMatchingTemplateInfo.SelectMany(group => group.Templates));
            Assert.IsNull(matchResult.UnambiguousTemplateGroup);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_DefaultLanguageDisambiguates()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"),
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                GetListCommandArgsFor("new list console"),
                defaultLanguage: null,
                TestContext.CancellationToken);

            Assert.IsTrue(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.IsNotNull(matchResult.UnambiguousTemplateGroup);
            Assert.HasCount(1, matchResult.TemplateGroupsWithMatchingTemplateInfo);
            Assert.HasCount(2, matchResult.TemplateGroupsWithMatchingTemplateInfo.Single().Templates);
            Assert.IsNotNull(matchResult.TemplateGroupsWithMatchingTemplateInfo.Single().Templates.Single(t => t.Identity == "Console.App.L1"));
            Assert.IsNotNull(matchResult.TemplateGroupsWithMatchingTemplateInfo.Single().Templates.Single(t => t.Identity == "Console.App.L2"));
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_InputLanguageIsPreferredOverDefault()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L1", groupIdentity: "Console.App.Test").WithTag("language", "L1"),
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.L2", groupIdentity: "Console.App.Test").WithTag("language", "L2")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                GetListCommandArgsFor("new list console --language L2"),
                defaultLanguage: null,
                TestContext.CancellationToken);

            Assert.IsTrue(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.IsNotNull(matchResult.UnambiguousTemplateGroup);
            Assert.HasCount(2, matchResult.TemplateGroupsWithMatchingTemplateInfo!.Single().Templates);
            Assert.HasCount(2, matchResult.UnambiguousTemplateGroup!.Templates);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_PartialMatch_HasLanguageMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                  .WithTag("language", "L1")
                  .WithTag("type", "project")
                  .WithBaselineInfo("app", "standard")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                GetListCommandArgsFor("new list console --language L2"),
                defaultLanguage: null,
                TestContext.CancellationToken);

            Assert.IsFalse(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.IsEmpty(matchResult.TemplateGroupsWithMatchingTemplateInfo);
            Assert.HasCount(1, matchResult.TemplateGroups);
            Assert.HasCount(1, matchResult.TemplateGroups.Single().Templates);
            Assert.IsTrue(matchResult.HasLanguageMismatch);
            Assert.IsFalse(matchResult.HasTypeMismatch);
            Assert.IsFalse(matchResult.HasBaselineMismatch);
            Assert.IsNull(matchResult.UnambiguousTemplateGroup);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_PartialMatch_HasContextMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                       .WithTag("language", "L1")
                       .WithTag("type", "project")
                       .WithBaselineInfo("app", "standard")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                GetListCommandArgsFor("new list console --type item"),
                defaultLanguage: null,
                TestContext.CancellationToken);

            Assert.IsFalse(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.IsEmpty(matchResult.TemplateGroupsWithMatchingTemplateInfo);
            Assert.HasCount(1, matchResult.TemplateGroups);
            Assert.HasCount(1, matchResult.TemplateGroups.Single().Templates);
            Assert.IsFalse(matchResult.HasLanguageMismatch);
            Assert.IsTrue(matchResult.HasTypeMismatch);
            Assert.IsFalse(matchResult.HasBaselineMismatch);
            Assert.IsNull(matchResult.UnambiguousTemplateGroup);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_PartialMatch_HasBaselineMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                       .WithTag("language", "L1")
                       .WithTag("type", "project")
                       .WithBaselineInfo("app", "standard")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                GetListCommandArgsFor("new list console --baseline core"),
                defaultLanguage: null,
                TestContext.CancellationToken);

            Assert.IsFalse(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.IsEmpty(matchResult.TemplateGroupsWithMatchingTemplateInfo);
            Assert.HasCount(1, matchResult.TemplateGroups);
            Assert.HasCount(1, matchResult.TemplateGroups.Single().Templates);
            Assert.IsFalse(matchResult.HasLanguageMismatch);
            Assert.IsFalse(matchResult.HasTypeMismatch);
            Assert.IsTrue(matchResult.HasBaselineMismatch);
            Assert.IsNull(matchResult.UnambiguousTemplateGroup);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_PartialMatch_HasMultipleMismatches()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                       .WithTag("language", "L1")
                       .WithTag("type", "project")
                       .WithBaselineInfo("app", "standard")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                GetListCommandArgsFor("new list console --language L2 --type item --baseline core"),
                defaultLanguage: null,
                TestContext.CancellationToken);

            Assert.IsFalse(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.IsEmpty(matchResult.TemplateGroupsWithMatchingTemplateInfo);
            Assert.HasCount(1, matchResult.TemplateGroups);
            Assert.HasCount(1, matchResult.TemplateGroups.Single().Templates);
            Assert.IsTrue(matchResult.HasLanguageMismatch);
            Assert.IsTrue(matchResult.HasTypeMismatch);
            Assert.IsTrue(matchResult.HasBaselineMismatch);
            Assert.IsNull(matchResult.UnambiguousTemplateGroup);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_NoMatch()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                       .WithTag("language", "L1")
                       .WithTag("type", "project")
                       .WithBaselineInfo("app", "standard")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                     GetListCommandArgsFor("new list zzzzz --language L1 --type project --baseline app"),
                     defaultLanguage: null,
                     TestContext.CancellationToken);

            Assert.IsFalse(matchResult.HasTemplateGroupMatches);
            Assert.IsFalse(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.IsEmpty(matchResult.TemplateGroupsWithMatchingTemplateInfo);
            Assert.IsEmpty(matchResult.TemplateGroups);
            Assert.IsFalse(matchResult.HasLanguageMismatch);
            Assert.IsFalse(matchResult.HasTypeMismatch);
            Assert.IsFalse(matchResult.HasBaselineMismatch);
            Assert.IsFalse(matchResult.HasClassificationMismatch);
            Assert.IsNull(matchResult.UnambiguousTemplateGroup);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_MatchByTags()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test1")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithClassifications("Common", "Test")
                    .WithBaselineInfo("app", "standard")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                     GetListCommandArgsFor("new list --tag Common"),
                     defaultLanguage: null,
                     TestContext.CancellationToken);

            Assert.IsTrue(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.HasCount(1, matchResult.TemplateGroupsWithMatchingTemplateInfo);
            Assert.HasCount(1, matchResult.TemplateGroupsWithMatchingTemplateInfo.Single().Templates);
            Assert.IsFalse(matchResult.HasLanguageMismatch);
            Assert.IsFalse(matchResult.HasTypeMismatch);
            Assert.IsFalse(matchResult.HasBaselineMismatch);
            Assert.IsFalse(matchResult.HasClassificationMismatch);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_MatchByTagsIgnoredOnNameMatch()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console1", name: "Long name for Console App Test", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithTag("language", "L1")
                    .WithTag("type", "project")
                    .WithClassifications("Common", "Test")
                    .WithBaselineInfo("app", "standard"),
                new MockTemplateInfo("console2", name: "Long name for Console App", identity: "Console.App.T2", groupIdentity: "Console.App.Test2")
                  .WithTag("language", "L1")
                  .WithTag("type", "project")
                  .WithClassifications("Common", "Test")
                  .WithBaselineInfo("app", "standard")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                     GetListCommandArgsFor("new list Test"),
                     defaultLanguage: null,
                     TestContext.CancellationToken);

            Assert.IsTrue(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.HasCount(1, matchResult.TemplateGroupsWithMatchingTemplateInfo);
            Assert.HasCount(1, matchResult.TemplateGroupsWithMatchingTemplateInfo.Single().Templates);
            Assert.IsFalse(matchResult.HasLanguageMismatch);
            Assert.IsFalse(matchResult.HasTypeMismatch);
            Assert.IsFalse(matchResult.HasBaselineMismatch);
            Assert.AreEqual("console1", matchResult.UnambiguousTemplateGroup?.Templates.Single().ShortNameList.Single());
            Assert.AreEqual("Console.App.T1", matchResult.UnambiguousTemplateGroup?.Templates.Single().Identity);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_MatchByTagsIgnoredOnShortNameMatch()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App Test", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                  .WithTag("language", "L1")
                  .WithTag("type", "project")
                  .WithClassifications("Common", "Test", "Console")
                  .WithBaselineInfo("app", "standard"),
                new MockTemplateInfo("cons", name: "Long name for Cons App", identity: "Console.App.T2", groupIdentity: "Console.App.Test2")
                  .WithTag("language", "L1")
                  .WithTag("type", "project")
                  .WithClassifications("Common", "Test", "Console")
                  .WithBaselineInfo("app", "standard")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                     GetListCommandArgsFor("new list Console"),
                     defaultLanguage: null,
                     TestContext.CancellationToken);

            Assert.IsTrue(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.HasCount(1, matchResult.TemplateGroupsWithMatchingTemplateInfo);
            Assert.HasCount(1, matchResult.TemplateGroupsWithMatchingTemplateInfo.Single().Templates);
            Assert.IsFalse(matchResult.HasLanguageMismatch);
            Assert.IsFalse(matchResult.HasTypeMismatch);
            Assert.IsFalse(matchResult.HasBaselineMismatch);
            Assert.AreEqual("console", matchResult.UnambiguousTemplateGroup?.Templates.Single().ShortNameList.Single());
            Assert.AreEqual("Console.App.T1", matchResult.UnambiguousTemplateGroup?.Templates.Single().Identity);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_MatchByTagsAndMismatchByOtherFilter()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                   .WithTag("language", "L1")
                   .WithTag("type", "project")
                   .WithClassifications("Common", "Test")
                   .WithBaselineInfo("app", "standard")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                     GetListCommandArgsFor("new list --language L2 --type item --tag Common"),
                     defaultLanguage: null,
                     TestContext.CancellationToken);

            Assert.IsFalse(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.IsEmpty(matchResult.TemplateGroupsWithMatchingTemplateInfo);
            Assert.IsTrue(matchResult.HasTemplateGroupMatches);
            Assert.HasCount(1, matchResult.TemplateGroups);
            Assert.HasCount(1, matchResult.TemplateGroups.Single().Templates);
            Assert.IsTrue(matchResult.HasLanguageMismatch);
            Assert.IsTrue(matchResult.HasTypeMismatch);
            Assert.IsFalse(matchResult.HasBaselineMismatch);
        }

        [TestMethod]
        [DataRow("TestAuthor", "Test", true)]
        [DataRow("TestAuthor", "Other", false)]
        [DataRow("TestAuthor", "TeST", true)]
        [DataRow("TestAuthor", "Teşt", false)]
        [DataRow("match_middle_test", "middle", true)]
        [DataRow("input", "İnput", false)]
        public async Task TestGetTemplateResolutionResult_AuthorMatch(string templateAuthor, string commandAuthor, bool matchExpected)
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test", author: templateAuthor)
                   .WithTag("language", "L1")
                   .WithTag("type", "project")
                   .WithClassifications("Common", "Test")
                   .WithBaselineInfo("app", "standard")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                     GetListCommandArgsFor($"new list console --author {commandAuthor}"),
                     defaultLanguage: null,
                     TestContext.CancellationToken);

            if (matchExpected)
            {
                Assert.IsTrue(matchResult.HasTemplateGroupWithTemplateInfoMatches);
                Assert.HasCount(1, matchResult.TemplateGroupsWithMatchingTemplateInfo);
                Assert.HasCount(1, matchResult.TemplateGroupsWithMatchingTemplateInfo.Single().Templates);
                Assert.IsFalse(matchResult.HasAuthorMismatch);
            }
            else
            {
                Assert.IsFalse(matchResult.HasTemplateGroupWithTemplateInfoMatches);
                Assert.IsEmpty(matchResult.TemplateGroupsWithMatchingTemplateInfo);
                Assert.IsTrue(matchResult.HasTemplateGroupMatches);
                Assert.HasCount(1, matchResult.TemplateGroups);
                Assert.HasCount(1, matchResult.TemplateGroups.Single().Templates);
                Assert.IsTrue(matchResult.HasAuthorMismatch);
            }

            Assert.IsFalse(matchResult.HasLanguageMismatch);
            Assert.IsFalse(matchResult.HasTypeMismatch);
            Assert.IsFalse(matchResult.HasBaselineMismatch);
        }

        [TestMethod]
        [DataRow("TestTag", "TestTag", true)]
        [DataRow("Tag1||Tag2", "Tag1", true)]
        [DataRow("Tag1||Tag2", "Tag", false)]
        [DataRow("", "Tag", false)]
        [DataRow("TestTag", "Other", false)]
        [DataRow("TestTag", "TeSTTag", true)]
        [DataRow("TestTag", "TeştTag", false)]
        [DataRow("match_middle_test", "middle", false)]
        [DataRow("input", "İnput", false)]
        public async Task TestGetTemplateResolutionResult_TagsMatch(string templateTags, string commandTag, bool matchExpected)
        {
            const string separator = "||";
            string[] templateTagsArray = templateTags.Split(separator);

            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test", author: "TemplateAuthor")
                   .WithTag("language", "L1")
                   .WithTag("type", "project")
                   .WithClassifications(templateTagsArray)
                   .WithBaselineInfo("app", "standard")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                GetListCommandArgsFor($"new list console --tag {commandTag}"),
                defaultLanguage: null,
                TestContext.CancellationToken);

            if (matchExpected)
            {
                Assert.IsTrue(matchResult.HasTemplateGroupWithTemplateInfoMatches);
                Assert.HasCount(1, matchResult.TemplateGroupsWithMatchingTemplateInfo);
                Assert.HasCount(1, matchResult.TemplateGroupsWithMatchingTemplateInfo.Single().Templates);
                Assert.IsFalse(matchResult.HasClassificationMismatch);
            }
            else
            {
                Assert.IsFalse(matchResult.HasTemplateGroupWithTemplateInfoMatches);
                Assert.IsEmpty(matchResult.TemplateGroupsWithMatchingTemplateInfo);
                Assert.IsTrue(matchResult.HasTemplateGroupMatches);
                Assert.HasCount(1, matchResult.TemplateGroups);
                Assert.HasCount(1, matchResult.TemplateGroups.Single().Templates);
                Assert.IsTrue(matchResult.HasClassificationMismatch);
            }

            Assert.IsFalse(matchResult.HasLanguageMismatch);
            Assert.IsFalse(matchResult.HasTypeMismatch);
            Assert.IsFalse(matchResult.HasBaselineMismatch);
            Assert.IsFalse(matchResult.HasAuthorMismatch);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_TemplateWithoutTypeShouldNotBeMatchedForContextFilter()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", name: "Long name for Console App", identity: "Console.App.T1", groupIdentity: "Console.App.Test")
                    .WithClassifications("Common", "Test")
            };

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader());
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                GetListCommandArgsFor("new list console --type item"),
                defaultLanguage: null,
                TestContext.CancellationToken);

            Assert.IsFalse(matchResult.HasTemplateGroupWithTemplateInfoMatches);
            Assert.IsEmpty(matchResult.TemplateGroupsWithMatchingTemplateInfo);
            Assert.IsTrue(matchResult.HasTemplateGroupMatches);
            Assert.HasCount(1, matchResult.TemplateGroups);
            Assert.HasCount(1, matchResult.TemplateGroups.Single().Templates);
            Assert.IsFalse(matchResult.HasLanguageMismatch);
            Assert.IsTrue(matchResult.HasTypeMismatch);
            Assert.IsFalse(matchResult.HasBaselineMismatch);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_ConstraintsMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", identity: "Console.App.T1")
                    .WithConstraints(new TemplateConstraintInfo("test", "no")),
                new MockTemplateInfo("console2", identity: "Console.App.T2")
                 .WithConstraints(new TemplateConstraintInfo("test", "bad-param"))
            };

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new TestConstraintFactory("test")) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse("new list");
            var args = new ListCommandArgs((ListCommand)parseResult.CommandResult.Command, parseResult);

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader(), new TemplateConstraintManager(settings));
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                args,
                defaultLanguage: null,
                TestContext.CancellationToken);

            Assert.AreEqual(2, matchResult.ContraintsMismatchGroupCount);
            Assert.IsEmpty(matchResult.TemplateGroupsWithMatchingTemplateInfoAndParameters);
            Assert.IsTrue(matchResult.HasTemplateGroupMatches);
            Assert.HasCount(2, matchResult.TemplateGroups);
        }

        [TestMethod]
        public async Task TestGetTemplateResolutionResult_IgnoreConstraintsMismatch()
        {
            List<ITemplateInfo> templatesToSearch = new()
            {
                new MockTemplateInfo("console", identity: "Console.App.T1")
                    .WithConstraints(new TemplateConstraintInfo("test", "no")),
                new MockTemplateInfo("console2", identity: "Console.App.T2")
                 .WithConstraints(new TemplateConstraintInfo("test", "bad-param"))
            };

            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: new[] { (typeof(ITemplateConstraintFactory), (IIdentifiedComponent)new TestConstraintFactory("test")) });
            IEngineEnvironmentSettings settings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            var myCommand = CliTestHostFactory.CreateNewCommand(host);

            ParseResult parseResult = myCommand.Parse("new list --ignore-constraints");
            var args = new ListCommandArgs((ListCommand)parseResult.CommandResult.Command, parseResult);

            ListTemplateResolver resolver = new(templatesToSearch, new MockHostSpecificDataLoader(), new TemplateConstraintManager(settings));
            TemplateResolutionResult matchResult = await resolver.ResolveTemplatesAsync(
                args,
                defaultLanguage: null,
                TestContext.CancellationToken);

            Assert.AreEqual(0, matchResult.ContraintsMismatchGroupCount);
            Assert.HasCount(2, matchResult.TemplateGroupsWithMatchingTemplateInfoAndParameters);
            Assert.IsTrue(matchResult.HasTemplateGroupMatches);
            Assert.HasCount(2, matchResult.TemplateGroups);
        }

        private static ListCommandArgs GetListCommandArgsFor(string commandInput)
        {
            ICliTemplateEngineHost host = CliTestHostFactory.GetVirtualHost(additionalComponents: BuiltInTemplatePackagesProviderFactory.GetComponents(RepoTemplatePackages));
            RootCommand rootCommand = new();
            var myCommand = CliTestHostFactory.CreateNewCommand(host);
            rootCommand.Add(myCommand);

            ParseResult parseResult = rootCommand.Parse(commandInput);
            return new ListCommandArgs((ListCommand)parseResult.CommandResult.Command, parseResult);
        }
    }
}
