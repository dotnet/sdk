namespace Microsoft.TemplateSearch.TemplateDiscovery.PackChecking.Reporting
{
    public class PreFilterResult
    {
        public string FilterId { get; set; }
        public bool IsFiltered { get; set; }
        public string Reason { get; set; }
    }
}
