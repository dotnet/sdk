// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace TestApp

open System

[<EntryPoint>]
let main argv =
    Console.WriteLine(TestLibrary.Helper.GetMessage())
    0
