using System;
using System.Reflection;

internal class StartupHook
{
    public static void Initialize()
    {
        Console.WriteLine($"Startup hook initialized in '{Assembly.GetEntryAssembly()?.GetName().Name}'");
    }
}
