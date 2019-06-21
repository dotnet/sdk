namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ISearchPackFilter
    {
        bool ShouldPackBeFiltered(string candidatePackName, string candidatePackVersion);
    }
}
