using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mutant.Chicken.Net4.UnitTests
{
    [TestClass]
    public class RegionTests
    {
        [TestMethod]
        public void VerifyRegionExclude()
        {
            string value = @"test value value x test foo bar";
            string expected = @"test  bar";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("value", "foo", false, false, false)  };
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
        public void VerifyRegionInclude()
        {
            string value = @"test value value x test foo bar";
            string expected = @"test   x test  bar";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("value", "foo", true, false, false) };
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
        public void VerifyRegionIncludeWhitespaceFixup()
        {
            string value = @"test value value x test foo bar";
            string expected = @"testx testbar";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("value", "foo", true, false, true) };
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
        public void VerifyRegionIncludeWhitespaceFixup2()
        {
            string value = @"Hello
    #begin foo
value
    #end
There";
            string expected = @"Hello
foo
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin", "#end", true, false, true) };
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
        public void VerifyRegionIncludeWholeLine()
        {
            string value = @"Hello
    #begin foo
value
    #end
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin", "#end", true, true, true) };
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
        public void VerifyNoRegion()
        {
            string value = @"Hello
    #begin foo
value
    #end
There";
            string expected = @"Hello
    #begin foo
value
    #end
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin2", "#end2", true, true, true) };
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
        public void VerifyTornRegion1()
        {
            string value = @"Hello
    #begin foo
value
    #end
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin", "#end", true, true, true) };
            EngineConfig cfg = new EngineConfig(VariableCollection.Environment(), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            Assert.IsTrue(processor.Run(input, output, 14));

            output.Position = 0;
            byte[] resultBytes = new byte[output.Length];
            output.Read(resultBytes, 0, resultBytes.Length);
            string actual = Encoding.UTF8.GetString(resultBytes);
            AssertEx.AreEqual(expected, actual);
        }

        [TestMethod]
        public void VerifyTornRegion2()
        {
            string value = @"Hello
    #begin foo
value
    #end
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin", "#end", true, true, true) };
            EngineConfig cfg = new EngineConfig(VariableCollection.Environment(), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            Assert.IsTrue(processor.Run(input, output, 36));

            output.Position = 0;
            byte[] resultBytes = new byte[output.Length];
            output.Read(resultBytes, 0, resultBytes.Length);
            string actual = Encoding.UTF8.GetString(resultBytes);
            AssertEx.AreEqual(expected, actual);
        }

        [TestMethod]
        public void VerifyTornRegion3()
        {
            string value = @"Hello
    #begin foo
value
    #end
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin", "#end", true, true, true) };
            EngineConfig cfg = new EngineConfig(VariableCollection.Environment(), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            Assert.IsTrue(processor.Run(input, output, 28));

            output.Position = 0;
            byte[] resultBytes = new byte[output.Length];
            output.Read(resultBytes, 0, resultBytes.Length);
            string actual = Encoding.UTF8.GetString(resultBytes);
            AssertEx.AreEqual(expected, actual);
        }

        [TestMethod]
        public void VerifyTinyPageRegion()
        {
            string value = @"Hello
    #begin foo
value
    #end
There";
            string expected = @"Hello
value
There";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Region("#begin", "#end", true, true, true) };
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
