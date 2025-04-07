// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;

Console.WriteLine("Started");

while (true)
{
    Print();
    Lib2.Print();
    Thread.Sleep(500);
}

void Print()
{
    Console.WriteLine("Waiting");
}
