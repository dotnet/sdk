// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Numerics;
using System.Security.Cryptography;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public class ComputeCssScope : Task
{
    [Required]
    public ITaskItem[] ScopedCssInput { get; set; }

    [Required]
    public string TargetName { get; set; }

    [Output]
    public ITaskItem[] ScopedCss { get; set; }

    public override bool Execute()
    {
        ScopedCss = new ITaskItem[ScopedCssInput.Length];

        for (var i = 0; i < ScopedCssInput.Length; i++)
        {
            var input = ScopedCssInput[i];
            var relativePath = input.ItemSpec.ToLowerInvariant().Replace("\\", "//");
            var scope = input.GetMetadata("CssScope");
            scope = !string.IsNullOrEmpty(scope) ? scope : GenerateScope(TargetName, relativePath);

            var outputItem = new TaskItem(input);
            outputItem.SetMetadata("CssScope", scope);
            ScopedCss[i] = outputItem;
        }

        return !Log.HasLoggedErrors;
    }

    private static string GenerateScope(string targetName, string relativePath)
    {
        var bytes = Encoding.UTF8.GetBytes(relativePath + targetName);
#if !NET9_0_OR_GREATER
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(bytes);
#else
        var hashBytes = SHA256.HashData(bytes);
#endif
        var builder = new StringBuilder();
        builder.Append("b-");

        builder.Append(ToBase36(hashBytes));

        return builder.ToString();
    }

    private static string ToBase36(byte[] hash)
    {
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";

        var result = new char[10];
#if NET9_0_OR_GREATER
        var dividend = BigInteger.Abs(new BigInteger([.. hash.AsSpan()[..9]]));
#else
        var dividend = BigInteger.Abs(new BigInteger([.. hash.AsSpan(0, 9)]));
#endif
        for (var i = 0; i < 10; i++)
        {
            dividend = BigInteger.DivRem(dividend, 36, out var remainder);
            result[i] = chars[(int)remainder];
        }

        return new string(result);
    }
}
