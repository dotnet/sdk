using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings.TemplateInfoReaders
{
    public static class TemplateInfoReaderVersion1_0_0_1
    {
        public static TemplateInfo FromJObject(JObject entry)
        {
            TemplateInfo info = TemplateInfoReaderVersion1_0_0_0.FromJObject(entry);
            info.HasScriptRunningPostActions = entry.ToBool(nameof(TemplateInfo.HasScriptRunningPostActions));

            return info;
        }
    }
}
