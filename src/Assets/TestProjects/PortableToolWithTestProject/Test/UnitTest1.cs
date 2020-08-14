using System;
using Xunit;
using consoledemo;

namespace XUnitTestProject1
{
    public class UnitTest1
    {
        [Fact(Skip = "Test few tests")]
        public void call_main_should_not_throw()
        {
            Program.Main(Array.Empty<string>());
        }
    }
}
