using System;

namespace Dnvm
{
    internal class ExceptionUtilities
    {
        public static Exception Unreachable => new("This location is thought to be unreachable");
    }
}