// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
public class AotXmlWriterTests
{
    [TestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public async Task XmlWritingPreservesEncodingAndAsyncSupport(bool utf8, bool indent)
    {
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Async = true,
            Encoding = utf8 ? new UTF8Encoding(false) : Encoding.Unicode,
            Indent = indent
        }))
        {
            await writer.WriteStartElementAsync(null, "Solution", null);
            await writer.WriteElementStringAsync(null, "Project", null, "A&B");
            await writer.WriteEndElementAsync();
            await writer.FlushAsync();
        }

        byte[] bytes = stream.ToArray();
        string xml = (utf8 ? Encoding.UTF8 : Encoding.Unicode).GetString(bytes);
        Assert.Contains(utf8 ? "encoding=\"utf-8\"" : "encoding=\"utf-16\"", xml);
        Assert.AreEqual(indent, xml.Contains(Environment.NewLine, StringComparison.Ordinal));
        Assert.Contains("A&amp;B", xml);
        if (!utf8)
        {
            Assert.AreSequenceEqual(Encoding.Unicode.GetPreamble(), bytes[..2]);
        }

        stream.Position = 0;
        var document = XDocument.Load(stream);
        Assert.AreEqual("A&B", document.Root!.Element("Project")!.Value);
    }
}
