using System;

namespace AppWithResxAndDesignercs
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine(TestWithNestedFolder.ResourcesImplicateIncludeBySdk.HelloWorld);
            Console.WriteLine(ResourceExplicitSetByUser.hello2);
        }
    }
}
