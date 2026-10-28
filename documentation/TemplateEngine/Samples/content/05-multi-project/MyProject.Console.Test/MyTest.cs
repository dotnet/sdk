using System;
using Xunit;
using MyProject.Console;

namespace MyProject.Console.Test
{
    public class MyProject_Console_UnitTest
    {
        [Fact]
        public void MyProject_Console_Test()
        {
            var name = Sample.GetName();
            Assert.Equal("Console and unit test demo",name);
        }
    }
}
