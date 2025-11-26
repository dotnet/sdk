using System;
using System.Threading;

public class B
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Started B");

        while (true)
        {
            Lib.Common();
            Thread.Sleep(500);
        }
    }
}
