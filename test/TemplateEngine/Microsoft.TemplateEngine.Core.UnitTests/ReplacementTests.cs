using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Engine;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class ReplacementTests : TestBase
    {
        [Fact]
        public void VerifyReplacement()
        {
            string value = @"test value test";
            string expected = @"test foo test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = {new Replacment("value", "foo", "ReplacementOperationId")};
            EngineConfig cfg = new EngineConfig(VariableCollection.Environment(), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyNoReplacement()
        {
            string value = @"test value test";
            string expected = @"test value test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacment("value2", "foo", "ReplacementOperationId") };
            EngineConfig cfg = new EngineConfig(VariableCollection.Environment(), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyTornReplacement()
        {
            string value = @"test value test";
            string expected = @"test foo test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacment("value", "foo", "ReplacementOperationId") };
            EngineConfig cfg = new EngineConfig(VariableCollection.Environment(), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 6);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }

        [Fact]
        public void VerifyTinyPageReplacement()
        {
            string value = @"test value test";
            string expected = @"test foo test";

            byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();

            IOperationProvider[] operations = { new Replacment("value", "foo", "ReplacementOperationId") };
            EngineConfig cfg = new EngineConfig(VariableCollection.Environment(), "${0}$");
            IProcessor processor = Processor.Create(cfg, operations);

            //Changes should be made
            bool changed = processor.Run(input, output, 1);
            Verify(Encoding.UTF8, output, changed, value, expected);
        }
    }
}
