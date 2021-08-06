using System;
using Coords;
using Posns;

namespace Managed
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            Coord a = new Coord();
            Coord b = new Coord(39.0, 80.0);
            Console.WriteLine("Managed code, testing coords: " + a.ToString() + " and " + b.ToString());
            Console.WriteLine("Coord distance was " + a.Distance(b));

            Posn x = new Posn();
            Posn y = new Posn(39.0, 80.0);
            Console.WriteLine("Managed code, testing posns: " + x.ToString() + " and " + y.ToString());
            Console.WriteLine("Posn distance was " + x.Distance(y));
        }
    }
}
