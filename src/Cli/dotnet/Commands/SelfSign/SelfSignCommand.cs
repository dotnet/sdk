// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CommandLine;
using System.IO;
using Microsoft.NET.Build.Tasks;

namespace Microsoft.DotNet.Cli.Commands.SelfSign;

internal static class SelfSignCommand
{
    public static int Run(ParseResult parseResult, SelfSignCommandDefinition definition)
    {
        bool force = parseResult.GetValue(definition.ForceOption);
        string[] paths = parseResult.GetValue(definition.PathsArgument) ?? [];
        int signed = 0;
        int skipped = 0;
        int failed = 0;

        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                foreach (string file in ElfSigner.EnumerateFilesWithoutLinks(path, link => Console.WriteLine($"Skipping symbolic link: {link}")))
                {
                    Sign(file);
                }
            }
            else if (File.Exists(path))
            {
                Sign(path);
            }
            else
            {
                Console.Error.WriteLine($"File or directory not found: {path}");
                failed++;
            }
        }

        Console.WriteLine($"Signed {signed} file(s); skipped {skipped}; failed {failed}.");
        return failed == 0 ? 0 : 1;

        void Sign(string file)
        {
            try
            {
                switch (ElfSigner.SignFileInPlace(file, force))
                {
                    case ElfSigner.SignOutcome.Signed:
                        signed++;
                        break;
                    case ElfSigner.SignOutcome.AlreadyValid:
                    case ElfSigner.SignOutcome.NotElf:
                        skipped++;
                        break;
                    case ElfSigner.SignOutcome.ForeignSignatureRetained:
                        Console.WriteLine($"Kept the existing signature: {file}");
                        skipped++;
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to sign {file}: {ex.Message}");
                failed++;
            }
        }
    }
}
