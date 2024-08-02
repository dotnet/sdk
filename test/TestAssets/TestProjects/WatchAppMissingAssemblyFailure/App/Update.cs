using System;

class DepSubType : Dep.DepType
{
    int F() => 1;
}

class Printer
{
    public static void Print()
        => Console.WriteLine("Hello!");
}

