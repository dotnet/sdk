// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateFiltering;
using Microsoft.TemplateEngine.Cli.TemplateResolution;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    [TestClass]
    public class TemplateMatchInfoTests
    {
        [TestMethod]
        public void EmptyMatchDisposition_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            Assert.IsFalse(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.IsFalse(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
            Assert.IsEmpty(templateMatchInfo.GetInvalidParameterNames());
            Assert.IsEmpty(templateMatchInfo.GetValidTemplateParameters());
        }

        [TestMethod]
        public void NameExactMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Exact));
            Assert.IsTrue(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.IsTrue(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [TestMethod]
        public void NamePartialMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Partial));
            Assert.IsTrue(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.IsTrue(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [TestMethod]
        public void NameMismatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Mismatch));
            Assert.IsFalse(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.IsFalse(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [TestMethod]
        public void TypeMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Type, "test", MatchKind.Exact));
            Assert.IsTrue(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.IsTrue(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [TestMethod]
        public void TypeMismatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Type, "test", MatchKind.Mismatch));
            Assert.IsFalse(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.IsFalse(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [TestMethod]
        public void TypeMatch_NameMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Exact));
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Type, "test", MatchKind.Exact));
            Assert.IsTrue(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.IsTrue(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [TestMethod]
        public void TypeMatch_NameMismatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Mismatch));
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Type, "test", MatchKind.Exact));
            Assert.IsFalse(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.IsTrue(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [TestMethod]
        public void TypeMatch_NamePartialMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Partial));
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Type, "test", MatchKind.Exact));
            Assert.IsTrue(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.IsTrue(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [TestMethod]
        public void TypeMismatch_NameMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Exact));
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Type, "test", MatchKind.Mismatch));
            Assert.IsFalse(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.IsTrue(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }

        [TestMethod]
        public void TypeMismatch_NamePartialMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new MockTemplateInfo();
            ITemplateMatchInfo templateMatchInfo = new TemplateMatchInfo(templateInfo);
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Name, "test", MatchKind.Partial));
            templateMatchInfo.AddMatchDisposition(new MatchInfo(MatchInfo.BuiltIn.Type, "test", MatchKind.Mismatch));
            Assert.IsFalse(WellKnownSearchFilters.MatchesAllCriteria(templateMatchInfo));
            Assert.IsTrue(WellKnownSearchFilters.MatchesAtLeastOneCriteria(templateMatchInfo));
        }
    }
}
