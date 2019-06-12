namespace Microsoft.TemplateSearch.Common
{
    public class TemplateIdentificationEntry
    {
        public TemplateIdentificationEntry(string identity, string groupIdentity)
        {
            Identity = identity;
            GroupIdentity = groupIdentity;
        }

        public string Identity { get; }
        public string GroupIdentity { get; }
    }
}
