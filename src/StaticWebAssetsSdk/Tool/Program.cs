// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.IO.Compression;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tool;

internal static class Program
{
    public static int Main(string[] args)
    {
        RootCommand rootCommand = new();
        Command brotli = new("brotli");

        Option<CompressionLevel> compressionLevelOption = new("-c")
        {
            DefaultValueFactory = (_) => CompressionLevel.SmallestSize,
            Description = "System.IO.Compression.CompressionLevel for the Brotli compression algorithm."
        };
        Option<List<string>> sourcesOption = new("-s")
        {
            Description = "A list of files to compress.",
            AllowMultipleArgumentsPerToken = false
        };
        Option<List<string>> outputsOption = new("-o")
        {
            Description = "The filenames to output the compressed file to.",
            AllowMultipleArgumentsPerToken = false
        };
        Option<int> maxDegreeOfParallelismOption = new("--max-degree-of-parallelism")
        {
            DefaultValueFactory = (_) => -1,
            Description = "The maximum number of concurrent compression operations."
        };

        brotli.Add(compressionLevelOption);
        brotli.Add(sourcesOption);
        brotli.Add(outputsOption);
        brotli.Add(maxDegreeOfParallelismOption);

        rootCommand.Add(brotli);

        brotli.SetAction((ParseResult parseResults) =>
        {
            var c = parseResults.GetValue(compressionLevelOption);
            var s = parseResults.GetValue(sourcesOption);
            var o = parseResults.GetValue(outputsOption);
            var m = parseResults.GetValue(maxDegreeOfParallelismOption);

            if (m == 0 || m < -1)
            {
                Console.Error.WriteLine("--max-degree-of-parallelism must be -1 or a positive integer.");
                return 1;
            }

            if (s.Count != o.Count)
            {
                Console.Error.WriteLine("The number of source files must match the number of output files.");
                return 1;
            }

            var failed = 0;
            Parallel.For(0, s.Count, new ParallelOptions { MaxDegreeOfParallelism = m }, i =>
            {
                var source = s[i];
                var output = o[i];
                try
                {
                    using var sourceStream = File.OpenRead(source);
                    using var fileStream = new FileStream(output, FileMode.Create);

                    using var stream = new BrotliStream(fileStream, c);
                    sourceStream.CopyTo(stream);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error compressing '{source}' into '{output}'");
                    Console.Error.WriteLine(ex.ToString());
                    Interlocked.Exchange(ref failed, 1);
                }
            });

            return failed;
        });

        return rootCommand.Parse(args).Invoke();
    }
}
