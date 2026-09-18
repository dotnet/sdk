// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;
using Microsoft.Dotnet.Installation.Internal;

if (args is ["--wait"])
{
    Console.WriteLine("ready");
    _ = Console.ReadLine();
    return 0;
}

var executable = Environment.ProcessPath!;
string identity;
using (var stream = File.OpenRead(executable))
{
    identity = DotnetupVersionMetadataReader.Read(stream);
}

if (args is ["--hold"])
{
    Console.WriteLine(identity);
    _ = Console.ReadLine();
    Console.WriteLine(identity);
    return 0;
}

if (args is not ["--version"])
{
    return 91;
}

var mode = File.ReadAllText(executable + ".mode");
var version = identity.Split('|')[0];
Console.Error.WriteLine($"SelfUpdateProcess mode: {mode}");
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
    "valid" or "stderr-flood" or "nonzero" => version + Environment.NewLine,
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