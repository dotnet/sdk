// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using System.Text;

if (args is ["--wait"])
{
    Console.WriteLine("ready");
    _ = Console.ReadLine();
    return 0;
}

var executable = Environment.ProcessPath!;
var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

if (args is ["--hold"])
{
    Console.WriteLine(version);
    _ = Console.ReadLine();
    Console.WriteLine(version);
    return 0;
}

if (args is not ["--version"])
{
    return 91;
}

var mode = File.Exists(executable + ".mode") ? File.ReadAllText(executable + ".mode") : "valid";
File.AppendAllText(executable + ".invocations", "--version\n");
Console.Error.WriteLine($"SelfUpdateProcess mode: {mode}");
if (mode == "stdin-eof")
{
    if (await Console.In.ReadToEndAsync() != string.Empty)
    {
        return 92;
    }
}

if (mode == "private-contract" &&
    (Environment.GetEnvironmentVariable("DOTNETUP_PRIVATE_VERSION_UTF8") != "1" ||
    Environment.GetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT") != "1" ||
    Environment.GetEnvironmentVariable("DOTNET_NOLOGO") != "1"))
{
    return 93;
}
if (mode == "timeout")
{
    File.WriteAllText(executable + ".pid", Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
    await Task.Delay(TimeSpan.FromMinutes(5));
    return 0;
}

var output = mode switch
{
    "empty" => "",
    "wrong" => new string('f', 64) + Environment.NewLine,
    "extra" => version + Environment.NewLine + version + Environment.NewLine,
    "space" => version + " " + Environment.NewLine,
    "bom" => "\uFEFF" + version,
    "lf" => version + "\n",
    "crlf" => version + "\r\n",
    "cr" => version + "\r",
    "none" => version,
    "flood" => new string('x', 4096 * 128) + version + Environment.NewLine,
    "valid" or "stderr-flood" or "nonzero" or "stdin-eof" or "private-contract" => version + Environment.NewLine,
    _ => throw new InvalidOperationException($"Unknown fixture mode: {mode}"),
};

if (mode is "flood" or "stderr-flood")
{
    Console.Error.Write(new string('x', 4096 * 128));
}
else if (mode == "nonzero")
{
    Console.Error.WriteLine("fixture failure");
}

var bytes = Encoding.UTF8.GetBytes(output);
using (var stdout = Console.OpenStandardOutput())
{
    stdout.Write(bytes);
    stdout.Flush();
}

// Record the bytes emitted by this invocation so rejection tests can independently check stdout.
File.WriteAllBytes(executable + ".stdout", bytes);
return mode == "nonzero" ? 17 : 0;