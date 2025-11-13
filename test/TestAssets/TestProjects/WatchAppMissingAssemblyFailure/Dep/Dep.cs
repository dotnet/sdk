// This attribute is not causing Dep.dll to be loaded, but enough
// to cause the HotReloadAgent to fail on getting custom attributes.
[assembly: Dep2.Test()]

namespace Dep;

public class DepLib
{
    public static void F()
    {
        Console.WriteLine(1);
    }
}
