// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
module FSharpAppWithLib.Lib

open System.Runtime.CompilerServices

[<MethodImpl(MethodImplOptions.NoInlining)>]
let message () = "LibWaiting"
