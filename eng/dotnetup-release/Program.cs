// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Http.Headers;
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
            string token = Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN") ?? throw new InvalidOperationException("SYSTEM_ACCESSTOKEN is required to verify the source build.");
            using HttpClient client = new();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage response = await client.GetAsync(new Uri($"https://dev.azure.com/dnceng/internal/_apis/build/builds/{args[2]}?api-version=7.1")).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            JsonElement build = document.RootElement;
            if (build.GetProperty("definition").GetProperty("id").GetInt32() != 1544
                || build.GetProperty("repository").GetProperty("id").GetString() != "7fa5dddb-89e8-4b26-8595-a6d15593e354"
                || build.GetProperty("status").GetString() != "completed" || build.GetProperty("result").GetString() != "succeeded"
                || build.GetProperty("sourceVersion").GetString() != args[3]
                || !build.GetProperty("tags").EnumerateArray().Any(tag => tag.GetString() == "PME Signed"))
            {
                throw new InvalidDataException("The selected resource must be a successful, PME-signed dotnet-dnup-official-ci build at the selected commit.");
            }

            Console.WriteLine($"DRY RUN: source build {args[2]}, branch {build.GetProperty("sourceBranch").GetString()}. No external writes will occur.");
            ReleasePlan plan = await ReleasePlanner.CreateAsync(args[0], args[1], args[2], args[3], new AkaOwnership(args[5], args[6], args[7])).ConfigureAwait(false);
            string json = JsonSerializer.Serialize(plan, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
            await File.WriteAllTextAsync(args[4], json + Environment.NewLine).ConfigureAwait(false);
            Console.WriteLine(json);
            return 0;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or JsonException or HttpRequestException or InvalidOperationException or CryptographicException)
        {
            Console.Error.WriteLine($"Release planning failed: {exception.Message}");
            return 1;
        }
    }
}
