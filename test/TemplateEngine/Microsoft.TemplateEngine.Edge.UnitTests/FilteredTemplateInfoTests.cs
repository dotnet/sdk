using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class FilteredTemplateInfoTests
    {
        [Fact(DisplayName = nameof(EmptyMatchDisposition_ReportsCorrectly))]
        public void EmptyMatchDisposition_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new TemplateInfo();
            IFilteredTemplateInfo filteredTemplateInfo = new FilteredTemplateInfo(templateInfo);
            Assert.False(filteredTemplateInfo.IsMatch);
            Assert.False(filteredTemplateInfo.IsMatchExceptContext);
            Assert.False(filteredTemplateInfo.IsPartialMatch);
            Assert.False(filteredTemplateInfo.IsPartialMatchExceptContext);
            Assert.False(filteredTemplateInfo.IsInvokableMatch);
            Assert.False(filteredTemplateInfo.HasAmbiguousParameterValueMatch);
            Assert.False(filteredTemplateInfo.HasParameterMismatch);
            Assert.False(filteredTemplateInfo.HasParseError);
            Assert.Equal(0, filteredTemplateInfo.InvalidParameterNames.Count);
            Assert.Equal(0, filteredTemplateInfo.ValidTemplateParameters.Count);
        }

        [Fact(DisplayName = nameof(NameExactMatch_ReportsCorrectly))]
        public void NameExactMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new TemplateInfo();
            IFilteredTemplateInfo filteredTemplateInfo = new FilteredTemplateInfo(templateInfo);
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Exact });
            Assert.True(filteredTemplateInfo.IsMatch);
            Assert.False(filteredTemplateInfo.IsMatchExceptContext);
            Assert.True(filteredTemplateInfo.IsPartialMatch);
            Assert.False(filteredTemplateInfo.IsPartialMatchExceptContext);
            Assert.True(filteredTemplateInfo.IsInvokableMatch);
            Assert.False(filteredTemplateInfo.HasAmbiguousParameterValueMatch);
            Assert.False(filteredTemplateInfo.HasParameterMismatch);
            Assert.False(filteredTemplateInfo.HasParseError);
        }

        [Fact(DisplayName = nameof(NamePartialMatch_ReportsCorrectly))]
        public void NamePartialMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new TemplateInfo();
            IFilteredTemplateInfo filteredTemplateInfo = new FilteredTemplateInfo(templateInfo);
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Partial });
            Assert.True(filteredTemplateInfo.IsMatch);
            Assert.False(filteredTemplateInfo.IsMatchExceptContext);
            Assert.True(filteredTemplateInfo.IsPartialMatch);
            Assert.False(filteredTemplateInfo.IsPartialMatchExceptContext);
            Assert.True(filteredTemplateInfo.IsInvokableMatch);
            Assert.False(filteredTemplateInfo.HasAmbiguousParameterValueMatch);
            Assert.False(filteredTemplateInfo.HasParameterMismatch);
            Assert.False(filteredTemplateInfo.HasParseError);
        }

        [Fact(DisplayName = nameof(NameMismatch_ReportsCorrectly))]
        public void NameMismatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new TemplateInfo();
            IFilteredTemplateInfo filteredTemplateInfo = new FilteredTemplateInfo(templateInfo);
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Mismatch });
            Assert.False(filteredTemplateInfo.IsMatch);
            Assert.False(filteredTemplateInfo.IsMatchExceptContext);
            Assert.False(filteredTemplateInfo.IsPartialMatch);
            Assert.False(filteredTemplateInfo.IsPartialMatchExceptContext);
            Assert.False(filteredTemplateInfo.IsInvokableMatch);
            Assert.False(filteredTemplateInfo.HasAmbiguousParameterValueMatch);
            Assert.False(filteredTemplateInfo.HasParameterMismatch);
            Assert.False(filteredTemplateInfo.HasParseError);
        }

        [Fact(DisplayName = nameof(ContextMatch_ReportsCorrectly))]
        public void ContextMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new TemplateInfo();
            IFilteredTemplateInfo filteredTemplateInfo = new FilteredTemplateInfo(templateInfo);
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Exact });
            Assert.True(filteredTemplateInfo.IsMatch);
            Assert.False(filteredTemplateInfo.IsMatchExceptContext);
            Assert.True(filteredTemplateInfo.IsPartialMatch);
            Assert.False(filteredTemplateInfo.IsPartialMatchExceptContext);
            Assert.True(filteredTemplateInfo.IsInvokableMatch);
            Assert.False(filteredTemplateInfo.HasAmbiguousParameterValueMatch);
            Assert.False(filteredTemplateInfo.HasParameterMismatch);
            Assert.False(filteredTemplateInfo.HasParseError);
        }

        [Fact(DisplayName = nameof(ContextMismatch_ReportsCorrectly))]
        public void ContextMismatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new TemplateInfo();
            IFilteredTemplateInfo filteredTemplateInfo = new FilteredTemplateInfo(templateInfo);
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Mismatch });
            Assert.False(filteredTemplateInfo.IsMatch);
            Assert.True(filteredTemplateInfo.IsMatchExceptContext);
            Assert.False(filteredTemplateInfo.IsPartialMatch);
            Assert.False(filteredTemplateInfo.IsPartialMatchExceptContext); // there must be another match info for this to be true
            Assert.False(filteredTemplateInfo.IsInvokableMatch);
            Assert.False(filteredTemplateInfo.HasAmbiguousParameterValueMatch);
            Assert.False(filteredTemplateInfo.HasParameterMismatch);
            Assert.False(filteredTemplateInfo.HasParseError);
        }

        [Fact(DisplayName = nameof(ContextMatch_NameMatch_ReportsCorrectly))]
        public void ContextMatch_NameMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new TemplateInfo();
            IFilteredTemplateInfo filteredTemplateInfo = new FilteredTemplateInfo(templateInfo);
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Exact });
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Exact });
            Assert.True(filteredTemplateInfo.IsMatch);
            Assert.False(filteredTemplateInfo.IsMatchExceptContext);
            Assert.True(filteredTemplateInfo.IsPartialMatch);
            Assert.False(filteredTemplateInfo.IsPartialMatchExceptContext);
            Assert.True(filteredTemplateInfo.IsInvokableMatch);
            Assert.False(filteredTemplateInfo.HasAmbiguousParameterValueMatch);
            Assert.False(filteredTemplateInfo.HasParameterMismatch);
            Assert.False(filteredTemplateInfo.HasParseError);
        }

        [Fact(DisplayName = nameof(ContextMatch_NameMismatch_ReportsCorrectly))]
        public void ContextMatch_NameMismatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new TemplateInfo();
            IFilteredTemplateInfo filteredTemplateInfo = new FilteredTemplateInfo(templateInfo);
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Mismatch });
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Exact });
            Assert.False(filteredTemplateInfo.IsMatch);
            Assert.False(filteredTemplateInfo.IsMatchExceptContext);
            Assert.True(filteredTemplateInfo.IsPartialMatch);
            Assert.False(filteredTemplateInfo.IsPartialMatchExceptContext);
            Assert.False(filteredTemplateInfo.IsInvokableMatch);
            Assert.False(filteredTemplateInfo.HasAmbiguousParameterValueMatch);
            Assert.False(filteredTemplateInfo.HasParameterMismatch);
            Assert.False(filteredTemplateInfo.HasParseError);
        }

        [Fact(DisplayName = nameof(ContextMatch_NamePartialMatch_ReportsCorrectly))]
        public void ContextMatch_NamePartialMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new TemplateInfo();
            IFilteredTemplateInfo filteredTemplateInfo = new FilteredTemplateInfo(templateInfo);
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Partial });
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Exact });
            Assert.True(filteredTemplateInfo.IsMatch);
            Assert.False(filteredTemplateInfo.IsMatchExceptContext);
            Assert.True(filteredTemplateInfo.IsPartialMatch);
            Assert.False(filteredTemplateInfo.IsPartialMatchExceptContext);
            Assert.True(filteredTemplateInfo.IsInvokableMatch);
            Assert.False(filteredTemplateInfo.HasAmbiguousParameterValueMatch);
            Assert.False(filteredTemplateInfo.HasParameterMismatch);
            Assert.False(filteredTemplateInfo.HasParseError);
        }

        [Fact(DisplayName = nameof(ContextMismatch_NameMatch_ReportsCorrectly))]
        public void ContextMismatch_NameMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new TemplateInfo();
            IFilteredTemplateInfo filteredTemplateInfo = new FilteredTemplateInfo(templateInfo);
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Exact });
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Mismatch });
            Assert.False(filteredTemplateInfo.IsMatch);
            Assert.True(filteredTemplateInfo.IsMatchExceptContext);
            Assert.True(filteredTemplateInfo.IsPartialMatch);
            Assert.True(filteredTemplateInfo.IsPartialMatchExceptContext);
            Assert.False(filteredTemplateInfo.IsInvokableMatch);
            Assert.False(filteredTemplateInfo.HasAmbiguousParameterValueMatch);
            Assert.False(filteredTemplateInfo.HasParameterMismatch);
            Assert.False(filteredTemplateInfo.HasParseError);
        }

        [Fact(DisplayName = nameof(ContextMismatch_NamePartialMatch_ReportsCorrectly))]
        public void ContextMismatch_NamePartialMatch_ReportsCorrectly()
        {
            ITemplateInfo templateInfo = new TemplateInfo();
            IFilteredTemplateInfo filteredTemplateInfo = new FilteredTemplateInfo(templateInfo);
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Name, Kind = MatchKind.Partial });
            filteredTemplateInfo.AddDisposition(new MatchInfo { Location = MatchLocation.Context, Kind = MatchKind.Mismatch });
            Assert.False(filteredTemplateInfo.IsMatch);
            Assert.True(filteredTemplateInfo.IsMatchExceptContext);
            Assert.True(filteredTemplateInfo.IsPartialMatch);
            Assert.True(filteredTemplateInfo.IsPartialMatchExceptContext);
            Assert.False(filteredTemplateInfo.IsInvokableMatch);
            Assert.False(filteredTemplateInfo.HasAmbiguousParameterValueMatch);
            Assert.False(filteredTemplateInfo.HasParameterMismatch);
            Assert.False(filteredTemplateInfo.HasParseError);
        }
    }
}
