using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestProject
{
	[TestClass]
	public sealed class Test1
	{
		[TestMethod]
		public void TestMethod1()
		{
			Assert.AreEqual(1, 1);
		}

		[TestMethod]
		public void TestMethod2()
		{
			Assert.AreEqual(1, 2);
		}
	}
}
