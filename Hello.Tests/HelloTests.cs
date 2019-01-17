using HelloWorld;
using Xunit;

namespace HelloTests
{
    public class HelloTests
    {
        [Fact]
        public void SuccessfulTest()
        {
            Assert.Equal(42, new Program().Magic);
        }
    }
}
