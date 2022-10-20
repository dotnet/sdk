using Newtonsoft.Json.Linq;

namespace BaselineComparer
{
    public class BaselineCommandData
    {
        public string Command { get; set; }
        public string RelativePath { get; set; }
        public string ReportFileName { get; set; }

        public static BaselineCommandData FromJObject(JObject source)
        {
            return new BaselineCommandData()
            {
                Command = source.GetValue(nameof(Command)).ToString(),
                RelativePath = source.GetValue(nameof(RelativePath)).ToString(),
                ReportFileName = source.GetValue(nameof(ReportFileName)).ToString()
            };
        }
    }
}
