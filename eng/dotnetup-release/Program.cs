// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text.Json;

namespace Microsoft.DotNet.Dotnetup.Release;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 8 || !int.TryParse(args[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int buildId) || buildId <= 0)
        {
            Console.Error.WriteLine("Usage: dotnetup-release <artifacts> <preview|ga> <build-id> <commit> <output.json> <aka-owners> <aka-actor> <aka-group-owner>");
            return 1;
        }

        try
        {
            Console.WriteLine($"DRY RUN: source build {args[2]}, commit {args[3]}. No external writes will occur.");
            ReleasePlan plan = await ReleasePlanner.CreateAsync(args[0], args[1], args[2], args[3], new AkaOwnership(args[5], args[6], args[7])).ConfigureAwait(false);
            string json = JsonSerializer.Serialize(plan, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
            await File.WriteAllTextAsync(args[4], json + Environment.NewLine).ConfigureAwait(false);
            Console.WriteLine(json);
            return 0;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or JsonException or InvalidOperationException or CryptographicException)
        {
            Console.Error.WriteLine($"Release planning failed: {exception.Message}");
            return 1;
        }
    }
}
