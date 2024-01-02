using System;
using library;

namespace codestyle_project
{
    class Program
    {
        static void Main(string[] args)
        {
            Speaker speaker = new Speaker();
            Console.WriteLine(speaker.SayHello("World"));
        }
    }
}
