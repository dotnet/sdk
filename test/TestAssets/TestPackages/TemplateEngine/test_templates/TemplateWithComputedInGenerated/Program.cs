using System;

namespace ConsoleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
#if (useFrameNav)
            Console.WriteLine($"Use frame navigation. JoinMacroTest: %VAL%");
#else
            Console.WriteLine($"Don't use frame navigation.");
#endif
        }
    }
}
