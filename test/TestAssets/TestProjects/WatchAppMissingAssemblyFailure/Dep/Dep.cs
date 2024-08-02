namespace Dep;

public class DepType
{
    void F()
    {
        Console.WriteLine(1);
    }
}

public static class UpdateHandler
{
    // Lock to avoid the updated Print method executing concurrently with the update handler.
    public static object Guard = new object();

    public static void UpdateApplication(Type[] types)
    {
        lock (Guard)
        {
            Console.WriteLine($"Dep Updated types: {(types == null ? "<null>" : types.Length == 0 ? "<empty>" : string.Join(",", types.Select(t => t.Name)))}");
        }
    }
}
