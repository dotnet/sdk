using System;

namespace Coords
{
    public sealed class Coord
    {
        public double X;
        public double Y;

        public Coord()
        {
            X = 0.0;
            Y = 0.0;
        }

        public Coord(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double Distance(Coord dest)
        {
            double deltaX = (this.X - dest.X);
            double deltaY = (this.Y - dest.Y);
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        public override string ToString()
        {
            return "(" + this.X + "," + this.Y + ")";
        }
    }
}
