using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings.TemplateInfoReaders
{
    public class TemplateInfoReaderVersion1_0_0_1 : TemplateInfoReaderVersion1_0_0_0
    {
        public static new TemplateInfo FromJObject(JObject jObject)
        {
            TemplateInfoReaderVersion1_0_0_1 reader = new TemplateInfoReaderVersion1_0_0_1(jObject);
            return reader.Read();
        }

        public TemplateInfoReaderVersion1_0_0_1(JObject jObject)
            : base(jObject)
        {
        }

        public override TemplateInfo Read()
        {
            TemplateInfo info = base.Read();
            info.HasScriptRunningPostActions = _jObject.ToBool(nameof(TemplateInfo.HasScriptRunningPostActions));

            return info;
        }
    }
}
