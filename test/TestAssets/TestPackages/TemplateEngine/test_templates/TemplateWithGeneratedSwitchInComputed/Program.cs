using System;

namespace ConsoleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if (useExtensionsNavigation)
            Console.WriteLine("Use extensions navigation.");
#else
            Console.WriteLine("Don't use extensions navigation.");
#endif
        }
    }
}
