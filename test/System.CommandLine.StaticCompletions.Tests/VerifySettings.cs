
namespace System.CommandLine.StaticCompletions.Tests;

using System.Runtime.CompilerServices;

public static class VerifyConfiguration
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifyDiffPlex.Initialize();
        Verifier.UseProjectRelativeDirectory("snapshots");
    }
}
