// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Portions of the code in this file were ported from the spectre.console by Patrik Svensson, Phil Scott, Nils Andresen
// https://github.com/spectreconsole/spectre.console/blob/main/src/Spectre.Console/Internal/Backends/Ansi/AnsiDetector.cs
// and from the supports-ansi project by Qingrong Ke
// https://github.com/keqingrong/supports-ansi/blob/master/index.js

using System.Text.RegularExpressions;

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// Works together with the <see cref="NativeMethods"/> to figure out if the current console is capable of using ANSI output codes.
/// </summary>
internal static class AnsiDetector
{
    private static readonly Regex[] TerminalsRegexes =
    {
        new("^xterm"), // xterm, PuTTY, Mintty
        new("^rxvt"), // RXVT
        new("^(?!eterm-color).*eterm.*"), // Accepts eterm, but not eterm-color, which does not support moving the cursor, see #9950.
        new("^screen"), // GNU screen, tmux
        new("tmux"), // tmux
        new("^vt100"), // DEC VT series
        new("^vt102"), // DEC VT series
        new("^vt220"), // DEC VT series
        new("^vt320"), // DEC VT series
        new("ansi"), // ANSI
        new("scoansi"), // SCO ANSI
        new("cygwin"), // Cygwin, MinGW
        new("linux"), // Linux console
        new("konsole"), // Konsole
        new("bvterm"), // Bitvise SSH Client
        new("^st-256color"), // Suckless Simple Terminal, st
        new("alacritty"), // Alacritty
    };

    public static bool IsAnsiSupported(string? termType)
        => !String.IsNullOrEmpty(termType) && TerminalsRegexes.Any(regex => regex.IsMatch(termType));
}
