// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
module FSharpAppWithLib.Program

open System.Runtime.CompilerServices
open System.Threading

[<MethodImpl(MethodImplOptions.NoInlining)>]
let decorate (text: string) = sprintf "App[%s]" text

[<EntryPoint>]
let main argv =
    while true do
        printfn "%s" (decorate (FSharpAppWithLib.Lib.message ()))
        Thread.Sleep(200)
    0
