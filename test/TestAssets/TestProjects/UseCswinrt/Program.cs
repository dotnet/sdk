using System;
using Windows.Data.Json;

namespace consolecswinrt
{
    class Program
    {
        static void Main(string[] args)
        {
            var rootObject = JsonObject.Parse("{\"greet\": \"Hello\"}");
            Console.WriteLine(rootObject["greet"]);
        }
    }
}
