using System;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.WriteLine("Args: " + string.Join(", ", args));
            Console.WriteLine("CWD: " + System.IO.Directory.GetCurrentDirectory());
        }
    }
}
