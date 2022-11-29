using System;

namespace MyProject.Con
{
    class Program
    {
        static void Main(string[] args)
        {
#if (DisplayCopywrite)
            string copyright = "(copyright)";
#endif
            string title = "My App Title";

#if (DisplayCopywrite)
            Console.WriteLine($"Copyright : {copyright}");
#endif
#if (DisplayTitle)
            Console.WriteLine($"Title : {title}");
#endif
#if (BackgroundGreyAndDisplayCopyright)
            Console.WriteLine($"BackgroundGreyAndDisplayCopyright evaulated to true");
#endif

        }
    }
}
