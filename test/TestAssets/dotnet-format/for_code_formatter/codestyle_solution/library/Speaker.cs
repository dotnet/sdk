using System;
using System.Collections.Generic;

namespace library
{
    public class Speaker
    {
        string lastName;

        public string SayHello(string name)
        {
            lastName = name;
            return $"Hello {this.lastName}.";
        }
    }
}
