using System;
using System.Collections.Generic;

namespace Microsoft.TemplateSearch.Common
{
    public class TemplateToPackMap
    {
        public static TemplateToPackMap FromPackToTemplateDictionary(IReadOnlyDictionary<string, PackToTemplateEntry> templateDictionary)
        {
            Dictionary<string, PackInfo> identityToPackMap = new Dictionary<string, PackInfo>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, PackInfo> groupIdentityToPackMap = new Dictionary<string, PackInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, PackToTemplateEntry> entry in templateDictionary)
            {
                PackInfo packAndVersion = new PackInfo(entry.Key, entry.Value.Version, entry.Value.TotalDownloads);

                foreach (TemplateIdentificationEntry templateIdentityInfo in entry.Value.TemplateIdentificationEntry)
                {
                    // Empty entries for the identity or group identity are authoring errors.
                    // Here, they're just filtered to prevent them from being matched as empty string.

                    if (!string.IsNullOrEmpty(templateIdentityInfo.Identity))
                    {
                        identityToPackMap[templateIdentityInfo.Identity] = packAndVersion;
                    }

                    if (!string.IsNullOrEmpty(templateIdentityInfo.GroupIdentity))
                    {
                        groupIdentityToPackMap[templateIdentityInfo.GroupIdentity] = packAndVersion;
                    }
                }
            }

            return new TemplateToPackMap(identityToPackMap, groupIdentityToPackMap);
        }

        protected TemplateToPackMap(Dictionary<string, PackInfo> identityToPackMap, Dictionary<string, PackInfo> groupIdentityToPackMap)
        {
            _identityToPackMap = identityToPackMap;
            _groupIdentityToPackMap = groupIdentityToPackMap;
        }

        private readonly IReadOnlyDictionary<string, PackInfo> _identityToPackMap;
        private readonly IReadOnlyDictionary<string, PackInfo> _groupIdentityToPackMap;

        public bool TryGetPackInfoForTemplateIdentity(string templateName, out PackInfo packAndVersion)
        {
            if (_identityToPackMap.TryGetValue(templateName, out packAndVersion))
            {
                return true;
            }

            packAndVersion = PackInfo.Empty;
            return false;
        }

        public bool TryGetPackInfoForTemplateGroupIdentity(string templateName, out PackInfo packAndVersion)
        {
            if (_groupIdentityToPackMap.TryGetValue(templateName, out packAndVersion))
            {
                return true;
            }

            packAndVersion = PackInfo.Empty;
            return false;
        }
    }
}
