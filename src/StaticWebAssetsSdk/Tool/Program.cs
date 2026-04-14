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

        brotli.Add(compressionLevelOption);
        brotli.Add(sourcesOption);
        brotli.Add(outputsOption);

        rootCommand.Add(brotli);

        Command zstd = new("zstd");

        Option<int> zstdLevelOption = new("-c")
        {
            DefaultValueFactory = (_) => 19,
            Description = "Compression level for the Zstandard compression algorithm (1-22, default: 19)."
        };
        Option<List<string>> zstdSourcesOption = new("-s")
        {
            Description = "A list of files to compress.",
            AllowMultipleArgumentsPerToken = false
        };
        Option<List<string>> zstdOutputsOption = new("-o")
        {
            Description = "The filenames to output the compressed file to.",
            AllowMultipleArgumentsPerToken = false
        };
        Option<List<string>> zstdDictionariesOption = new("-d")
        {
            Description = "A list of dictionary files, one per source. Use empty string for sources without a dictionary.",
            AllowMultipleArgumentsPerToken = false
        };

        zstd.Add(zstdLevelOption);
        zstd.Add(zstdSourcesOption);
        zstd.Add(zstdOutputsOption);
        zstd.Add(zstdDictionariesOption);

        rootCommand.Add(zstd);

        zstd.SetAction((ParseResult parseResults) =>
        {
            var level = parseResults.GetValue(zstdLevelOption);
            var sources = parseResults.GetValue(zstdSourcesOption);
            var outputs = parseResults.GetValue(zstdOutputsOption);
            var dictionaries = parseResults.GetValue(zstdDictionariesOption);

            if (level < 1 || level > 22)
            {
                Console.Error.WriteLine($"Compression level {level} is out of range. Must be between 1 and 22.");
                return 1;
            }

            if (sources.Count != outputs.Count)
            {
                Console.Error.WriteLine($"Source count ({sources.Count}) does not match output count ({outputs.Count}).");
                return 1;
            }

            if (dictionaries != null && dictionaries.Count != sources.Count)
            {
                Console.Error.WriteLine($"Dictionary count ({dictionaries.Count}) does not match source count ({sources.Count}).");
                return 1;
            }

            var failed = 0;
            Parallel.For(0, sources.Count, i =>
            {
                var source = sources[i];
                var output = outputs[i];
                var dictionaryPath = dictionaries != null && i < dictionaries.Count ? dictionaries[i] : null;
                var tempOutput = output + ".tmp";
                try
                {
                    ZstandardCompressionOptions options;
                    if (!string.IsNullOrEmpty(dictionaryPath))
                    {
                        var dictBytes = File.ReadAllBytes(dictionaryPath);
                        var dictionary = ZstandardDictionary.Create(dictBytes);
                        options = new ZstandardCompressionOptions { Quality = level, Dictionary = dictionary };
                    }
                    else
                    {
                        options = new ZstandardCompressionOptions { Quality = level };
                    }

                    using (var sourceStream = File.OpenRead(source))
                    using (var fileStream = new FileStream(tempOutput, FileMode.Create))
                    using (var stream = new ZstandardStream(fileStream, options))
                    {
                        sourceStream.CopyTo(stream);
                    }

                    File.Move(tempOutput, output, overwrite: true);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error compressing '{source}' into '{output}'");
                    Console.Error.WriteLine(ex.ToString());
                    try { File.Delete(tempOutput); } catch { }
                    Interlocked.Increment(ref failed);
                }
            });

            return failed > 0 ? 1 : 0;
        });

        brotli.SetAction((ParseResult parseResults) =>
        {
            var compressionLevel = parseResults.GetValue(compressionLevelOption);
            var sources = parseResults.GetValue(sourcesOption);
            var outputs = parseResults.GetValue(outputsOption);

            if (sources.Count != outputs.Count)
            {
                Console.Error.WriteLine($"Source count ({sources.Count}) does not match output count ({outputs.Count}).");
                return 1;
            }

            var failed = 0;
            Parallel.For(0, sources.Count, i =>
            {
                var source = sources[i];
                var output = outputs[i];
                var tempOutput = output + ".tmp";
                try
                {
                    using (var sourceStream = File.OpenRead(source))
                    using (var fileStream = new FileStream(tempOutput, FileMode.Create))
                    using (var stream = new BrotliStream(fileStream, compressionLevel))
                    {
                        sourceStream.CopyTo(stream);
                    }

                    File.Move(tempOutput, output, overwrite: true);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error compressing '{source}' into '{output}'");
                    Console.Error.WriteLine(ex.ToString());
                    try { File.Delete(tempOutput); } catch { }
                    Interlocked.Increment(ref failed);
                }
            });

            return failed > 0 ? 1 : 0;
        });

        return rootCommand.Parse(args).Invoke();
    }
}
