using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Blittable RM_UNIQUE_PROCESS identity; FILETIME retains native four-byte alignment.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RestartManagerUniqueProcess
{
    public uint ProcessId;
    public FILETIME ProcessStartTime;
}