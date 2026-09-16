// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;
using Microsoft.Dotnet.Installation.Internal;

var executable = Environment.ProcessPath!;
string identity;
using (var stream = File.OpenRead(executable))
{
    identity = DotnetupBuildIdentityReader.Read(stream);
}

if (args is ["--hold"])
{
    Console.WriteLine(identity);
    _ = Console.ReadLine();
    Console.WriteLine(identity);
    return 0;
}

if (args is not ["--build-identity"])
{
    return 91;
}

var mode = File.ReadAllText(executable + ".mode");
switch (mode)
{
    case "timeout":
        File.WriteAllText(executable + ".pid", Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        await Task.Delay(TimeSpan.FromMinutes(5));
        return 0;
    case "flood":
    case "stderr-flood":
        var output = new string('x', 4096);
        for (var index = 0; index < 128; index++)
        {
            Console.Error.Write(output);
            if (mode == "flood")
            {
                Console.Write(output);
            }
        }
        Console.WriteLine(identity);
        return 0;
    case "empty":
        return 0;
    case "wrong":
        Console.WriteLine(new string('f', 64));
        return 0;
    case "upper":
        Console.WriteLine(identity.ToUpperInvariant());
        return 0;
    case "extra":
        Console.WriteLine(identity);
        Console.WriteLine(identity);
        return 0;
    case "space":
        Console.WriteLine(identity + " ");
        return 0;
    case "bom":
        using (var stdout = Console.OpenStandardOutput())
        {
            stdout.Write(new byte[] { 0xef, 0xbb, 0xbf });
            stdout.Write(Encoding.ASCII.GetBytes(identity));
        }
        return 0;
    case "lf":
        Console.Write(identity + "\n");
        return 0;
    case "crlf":
        Console.Write(identity + "\r\n");
        return 0;
    case "cr":
        Console.Write(identity + "\r");
        return 0;
    case "none":
        Console.Write(identity);
        return 0;
    case "nonzero":
        Console.Error.WriteLine("fixture failure");
        Console.WriteLine(identity);
        return 17;
    default:
        Console.WriteLine(identity);
        return 0;
}