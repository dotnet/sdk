namespace Microsoft.ComponentDetection.Detectors.NuGet;

using static global::NuGet.Frameworks.FrameworkConstants.CommonFrameworks;

/// <summary>
/// Framework packages for .NETFramework,Version=v4.6.1.
/// </summary>
internal partial class FrameworkPackages
{
    internal static class NET461
    {
        internal static FrameworkPackages Instance { get; } = new(Net461, DefaultFrameworkKey, NETStandard20.Instance);

        internal static void Register() => FrameworkPackages.Register(Instance);
    }
}
