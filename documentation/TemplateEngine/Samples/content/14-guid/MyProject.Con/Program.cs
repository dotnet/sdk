using System;

namespace MyProject.Con
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(@"
My ID (N): myid01
My ID (D): myid02
My ID (B): myid03
My ID (P): myid04
My ID (X): myid05

// guids can have different formats. By default we will search for guids matching all formats and replacing them with the new guid value,
// and match the format used. Below you'll see the same guid is replaced by a new guid, and the format is preserved.
// More info on guid formats at https://msdn.microsoft.com/en-us/library/97af8hh4(v=vs.110).aspx

N: 12aa8f4ea4aa4ac1927c94cb99485ef1
D: 12aa8f4e-a4aa-4ac1-927c-94cb99485ef1
B: {12aa8f4e-a4aa-4ac1-927c-94cb99485ef1}
P: (12aa8f4e-a4aa-4ac1-927c-94cb99485ef1)
X: {0x12aa8f4e,0xa4aa,0x4ac1,{0x92,0x7c,0x94,0xcb,0x99,0x48,0x5e,0xf1}}
");
        }
    }
}
