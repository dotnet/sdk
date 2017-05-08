// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace TestLibrary

open System

module Helper =
    /// <summary>
    /// Gets the message from the helper. This comment is here to help test XML documentation file generation, please do not remove it.
    /// </summary>
    /// <returns>A message</returns>
    let GetMessage () = "This string came from the test library!"
    let SayHi () = Console.WriteLine("Hello there!")
