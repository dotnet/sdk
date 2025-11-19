using System.Text.Json.Serialization;

namespace Microsoft.DotNet.Tools.Bootstrapper;

public class GlobalJsonContents
{
    public SdkSection? Sdk { get; set; }

    public class SdkSection
    {
        public string? Version { get; set; }
        public bool? AllowPrerelease { get; set; }
        public string? RollForward { get; set; }
        public string[]? Paths { get; set; }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GlobalJsonContents))]
public partial class GlobalJsonContentsJsonContext : JsonSerializerContext
{
}
