using System;
using System.Linq;
using System.Reflection;
using System.Threading;

public class A
{
    public static void Main(string[] args)
    {
        var attrValue = typeof(A).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "TestAssemblyMetadata").Value;
        Console.WriteLine($"Started A: {attrValue}");

        while (true)
        {
            Lib.Common();
            Thread.Sleep(500);
        }
    }
}
