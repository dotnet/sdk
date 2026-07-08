using System.Threading.Tasks;
using Lib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestProject
{
	[TestClass]
	public sealed class Test1
	{
		private readonly AsyncCalculator _calculator = new();

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

		[TestMethod]
		public async Task AddAsync_Works()
		{
			int result = await _calculator.AddAsync(2, 3);
			Assert.AreEqual(5, result);
		}

		[TestMethod]
		public async Task SubtractAsync_Works()
		{
			int result = await _calculator.SubtractAsync(10, 4);
			Assert.AreEqual(6, result);
		}

		[TestMethod]
		public async Task MultiplyAsync_Works()
		{
			int result = await _calculator.MultiplyAsync(3, 4);
			Assert.AreEqual(12, result);
		}

		[TestMethod]
		public async Task EnumerateAsync_Works()
		{
			int sum = 0;
			await foreach (int i in _calculator.EnumerateAsync(5))
			{
				sum += i;
			}
			Assert.AreEqual(10, sum);
		}
	}
}
