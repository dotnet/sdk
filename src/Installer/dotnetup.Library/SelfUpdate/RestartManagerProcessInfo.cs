using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Blittable RM_PROCESS_INFO buffer with inline UTF-16 strings and a four-byte native BOOL.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct RestartManagerProcessInfo
{
    public RestartManagerUniqueProcess Process;
    public fixed char ApplicationName[256];
    public fixed char ServiceShortName[64];
    public uint ApplicationType;
    public uint ApplicationStatus;
    public uint SessionId;
    public int Restartable;
}