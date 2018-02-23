using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Edge.Settings.TemplateInfoReaders
{
    public class TemplateInfoReaderVersion1_0_0_2 : TemplateInfoReaderVersion1_0_0_1
    {
        public static new TemplateInfo FromJObject(JObject jObject)
        {
            TemplateInfo info = new TemplateInfo();

            TemplateInfoReaderVersion1_0_0_2 reader = new TemplateInfoReaderVersion1_0_0_2(jObject);
            return reader.Read();
        }

        public TemplateInfoReaderVersion1_0_0_2(JObject jObject)
            : base(jObject)
        {
        }

        protected override ICacheTag ReadOneTag(JProperty item)
        {
            ICacheTag cacheTag = base.ReadOneTag(item);

            if (cacheTag is IAllowDefaultIfOptionWithoutValue tagWithNoValueDefault)
            {
                tagWithNoValueDefault.DefaultIfOptionWithoutValue = item.Value.ToString(nameof(IAllowDefaultIfOptionWithoutValue.DefaultIfOptionWithoutValue));
                return tagWithNoValueDefault as CacheTag;
            }

            return cacheTag;
        }

        protected override ICacheParameter ReadOneParameter(JProperty item)
        {
            ICacheParameter param = base.ReadOneParameter(item);

            if (param is IAllowDefaultIfOptionWithoutValue paramWithNoValueDefault)
            {
                paramWithNoValueDefault.DefaultIfOptionWithoutValue = item.Value.ToString(nameof(IAllowDefaultIfOptionWithoutValue.DefaultIfOptionWithoutValue));
                return paramWithNoValueDefault as CacheParameter;
            }

            return param;
        }
    }
}
