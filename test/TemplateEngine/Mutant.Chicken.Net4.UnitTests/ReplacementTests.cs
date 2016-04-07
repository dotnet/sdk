using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mutant.Chicken.Net4.UnitTests
{
    [TestClass]
    public class ReplacementTests
    {
        [TestMethod]
        public void VerifyReplacement()
        {
            string value = @"test value test";
            string expected = @"test foo test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = {new Replacment("value", "foo")};
            EngineConfig cfg = new EngineConfig(VariableCollection.Environment(), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);
            
            //Changes should be made
            Assert.IsTrue(processor.Run(input, output));

            output.Position = 0;
            byte[] resultBytes = new byte[output.Length];
            output.Read(resultBytes, 0, resultBytes.Length);
            string actual = Encoding.UTF8.GetString(resultBytes);
            AssertEx.AreEqual(expected, actual);
        }

        [TestMethod]
        public void VerifyNoReplacement()
        {
            string value = @"test value test";
            string expected = @"test value test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacment("value2", "foo") };
            EngineConfig cfg = new EngineConfig(VariableCollection.Environment(), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            Assert.IsFalse(processor.Run(input, output));

            output.Position = 0;
            byte[] resultBytes = new byte[output.Length];
            output.Read(resultBytes, 0, resultBytes.Length);
            string actual = Encoding.UTF8.GetString(resultBytes);
            AssertEx.AreEqual(expected, actual);
        }

        [TestMethod]
        public void VerifyTornReplacement()
        {
            string value = @"test value test";
            string expected = @"test foo test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacment("value", "foo") };
            EngineConfig cfg = new EngineConfig(VariableCollection.Environment(), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            Assert.IsTrue(processor.Run(input, output, 6));

            output.Position = 0;
            byte[] resultBytes = new byte[output.Length];
            output.Read(resultBytes, 0, resultBytes.Length);
            string actual = Encoding.UTF8.GetString(resultBytes);
            AssertEx.AreEqual(expected, actual);
        }

        [TestMethod]
        public void VerifyTinyPageReplacement()
        {
            string value = @"test value test";
            string expected = @"test foo test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacment("value", "foo") };
            EngineConfig cfg = new EngineConfig(VariableCollection.Environment(), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            Assert.IsTrue(processor.Run(input, output, 1));

            output.Position = 0;
            byte[] resultBytes = new byte[output.Length];
            output.Read(resultBytes, 0, resultBytes.Length);
            string actual = Encoding.UTF8.GetString(resultBytes);
            AssertEx.AreEqual(expected, actual);
        }
    }
}
