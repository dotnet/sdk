using System.Diagnostics;
using System.Reflection.Metadata;

[assembly: MetadataUpdateHandler(typeof(UpdateHandler))]

// delete the dependency dll to cause load failure of DepSubType
var depPath = Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location!)!, "Dep.dll");
File.Delete(depPath);
Console.WriteLine($"File deleted: {depPath}");

while (true)
{
    Printer.Print();
    Thread.Sleep(100);
}

static class UpdateHandler
{
    public static void UpdateApplication(Type[] types)
    {
        Console.WriteLine($"Updated types: {(types == null ? "<null>" : types.Length == 0 ? "<empty>" : string.Join(",", types.Select(t => t.Name)))}");
    }
}
