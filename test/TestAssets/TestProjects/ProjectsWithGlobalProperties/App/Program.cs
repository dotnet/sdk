using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

if (args.Length != 1 ||
    !string.Equals(args[0], "one", StringComparison.OrdinalIgnoreCase) &&
    !string.Equals(args[0], "two", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Usage: zEntry <one|two>");
    return 1;
}

var instanceName = char.ToUpperInvariant(args[0][0]) + args[0][1..].ToLowerInvariant();
var assemblyPath = Path.Combine(AppContext.BaseDirectory, instanceName, "Library.dll");
var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
var libraryType = assembly.GetType("Library.LibraryClass", throwOnError: true)!;
var getMessage = libraryType.GetMethod(
    "GetMessage",
    BindingFlags.Public | BindingFlags.Static)!;

Console.WriteLine(getMessage.Invoke(null, parameters: null));
return 0;
