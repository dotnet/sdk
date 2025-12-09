namespace Microsoft.DotNet.HotReload;

internal readonly struct StaticAssetUpdate(
    string assemblyName,
    string relativePath,
    byte[] contents,
    bool isApplicationProject)
{
    public string AssemblyName { get; } = assemblyName;
    public bool IsApplicationProject { get; } = isApplicationProject;
    public string RelativePath { get; } = relativePath;
    public byte[] Contents { get; } = contents;
}
