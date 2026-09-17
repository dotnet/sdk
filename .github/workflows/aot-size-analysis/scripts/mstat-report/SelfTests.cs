// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Xml;

namespace MstatReport;

internal static class SelfTests
{
    public static void Run()
    {
        string directory = Path.Combine(
            AppContext.BaseDirectory,
            $"self-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string mstatPath = Path.Combine(directory, "tiny.mstat");
            string graphPath = Path.Combine(directory, "scan.dgml.xml");
            string htmlPath = Path.Combine(directory, "report.html");
            string genericCsvPath = Path.Combine(directory, "generic.csv");
            string singleCsvPath = Path.Combine(directory, "single.csv");
            SyntheticMstatWriter.Write(mstatPath);
            WriteGraph(graphPath);

            MstatModel mstat = MstatReader.Read(mstatPath);
            Assert(mstat.Version.Major == 2 && mstat.Version.Minor == 2, "MSTAT version");
            Assert(mstat.Methods.Count == 2, "method record count");
            Assert(mstat.Types.Count == 3, "type record count");
            Assert(mstat.DeduplicatedMethods.Count == 1, "deduplicated method count");
            Assert(
                mstat.Names.GetMember(mstat.Methods[1].Token).Text.Contains(
                    "Echo<int>(int): int",
                    StringComparison.Ordinal),
                "generic method name");

            SizeReport report = ReportBuilder.Build(mstat, mstatPath);
            Assert(report.AttributedSize == 71, "physical size sum");
            SizeNode typeInstantiation = Descendants(report.Root)
                .Single(static node => node.Kind == "type instantiation");
            Assert(typeInstantiation.Name == "Box<int>", "generic type name");
            SizeNode[] physicalTypeNodes = Descendants(typeInstantiation)
                .Where(static node => node.Kind == "type instantiation physical node")
                .ToArray();
            Assert(physicalTypeNodes.Length == 2, "split physical type nodes");
            Assert(
                physicalTypeNodes.Sum(static node => node.ExclusiveSize) == 16,
                "generic type physical size");

            report.Retention = RetentionGraphReader.Read(graphPath, report.Root);
            SizeNode genericMethod = Descendants(report.Root)
                .Single(static node => node.Kind == "method instantiation");
            Assert(genericMethod.ExclusiveSize == 16, "method code, GC, and EH size");
            Assert(
                genericMethod.RetentionIds is [2, 10],
                "duplicate retention label mapping");
            Assert(
                report.Retention.DirectEdges.Count(
                    static edge => edge.Target is 2 or 10) == 3,
                "all direct incoming edges");
            Assert(
                physicalTypeNodes.All(
                    static node => node.RetentionIds is [7] or [9]),
                "split physical retention mapping");
            Assert(
                report.Retention.RootParents[2].Source == 6,
                "shortest root path");

            ReportWriter.Write(report, htmlPath, genericCsvPath, singleCsvPath);
            string html = File.ReadAllText(htmlPath);
            string genericCsv = File.ReadAllText(genericCsvPath);
            string singleCsv = File.ReadAllText(singleCsvPath);
            Assert(html.Contains("Echo\\u003Cint\\u003E", StringComparison.Ordinal), "HTML label");
            Assert(html.Contains("Why is this kept?", StringComparison.Ordinal), "HTML retention UI");
            Assert(genericCsv.Contains("Box<int>", StringComparison.Ordinal), "generic CSV type");
            Assert(genericCsv.Contains("Echo<int>(int): int", StringComparison.Ordinal), "generic CSV method");
            Assert(!singleCsv.Contains("Echo<int>(int): int", StringComparison.Ordinal), "fan-in exclusion");
            Assert(singleCsv.Contains("keeps type", StringComparison.Ordinal), "single dependency CSV");
            Assert(File.ReadLines(singleCsvPath).Count() == 7, "complete single dependency CSV");

            string unsafeGraph = Path.Combine(directory, "unsafe.dgml.xml");
            File.WriteAllText(
                unsafeGraph,
                "<!DOCTYPE DirectedGraph [<!ENTITY external SYSTEM \"file:///does-not-exist\">]>" +
                "<DirectedGraph><Nodes><Node Id=\"0\" Label=\"&external;\"/></Nodes></DirectedGraph>");
            AssertThrows<XmlException>(
                () => RetentionGraphReader.Read(unsafeGraph, report.Root),
                "DTD prohibition");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static IEnumerable<SizeNode> Descendants(SizeNode node)
    {
        yield return node;
        foreach (SizeNode child in node.Children ?? [])
        {
            foreach (SizeNode descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static void WriteGraph(string path)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true
        };
        using XmlWriter writer = XmlWriter.Create(path, settings);
        writer.WriteStartElement("DirectedGraph", "http://schemas.microsoft.com/vs/2009/dgml");
        writer.WriteStartElement("Nodes");
        WriteNode(writer, 0, "AOT root");
        WriteNode(writer, 1, "Intermediate");
        WriteNode(writer, 2, "METHOD_INT");
        WriteNode(writer, 3, "TYPE<&>");
        WriteNode(writer, 4, "FIELD_VALUE");
        WriteNode(writer, 5, "FROZEN");
        WriteNode(writer, 6, "Second root");
        WriteNode(writer, 7, "BOX_INT");
        WriteNode(writer, 8, "METHOD_DEF");
        WriteNode(writer, 9, "BOX_INT_ALT");
        WriteNode(writer, 10, "METHOD_INT");
        writer.WriteEndElement();
        writer.WriteStartElement("Links");
        WriteLink(writer, 0, 1, "root to intermediate");
        WriteLink(writer, 1, 2, "intermediate to method");
        WriteLink(writer, 6, 2, "direct second root");
        WriteLink(writer, 0, 3, "keeps type");
        WriteLink(writer, 0, 4, "keeps field");
        WriteLink(writer, 0, 5, "keeps object");
        WriteLink(writer, 0, 7, "keeps type instantiation");
        WriteLink(writer, 0, 8, "keeps method");
        WriteLink(writer, 0, 9, "keeps alternate type node");
        WriteLink(writer, 0, 10, "keeps duplicate method label");
        writer.WriteEndElement();
        writer.WriteEndElement();

        static void WriteNode(XmlWriter writer, int id, string label)
        {
            writer.WriteStartElement("Node");
            writer.WriteAttributeString("Id", id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteAttributeString("Label", label);
            writer.WriteEndElement();
        }

        static void WriteLink(XmlWriter writer, int source, int target, string reason)
        {
            writer.WriteStartElement("Link");
            writer.WriteAttributeString(
                "Source",
                source.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteAttributeString(
                "Target",
                target.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writer.WriteAttributeString("Reason", reason);
            writer.WriteEndElement();
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Self-test failed: {message}.");
        }
    }

    private static void AssertThrows<T>(Action action, string message)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }

        throw new InvalidOperationException($"Self-test failed: {message} did not throw {typeof(T).Name}.");
    }
}

internal static class SyntheticMstatWriter
{
    public static void Write(string path)
    {
        var metadata = new MetadataBuilder();
        var ilStream = new BlobBuilder();
        var methodBodies = new MethodBodyStreamEncoder(ilStream);
        ReservedBlob<GuidHandle> mvidReservation = metadata.ReserveGuid();
        Blob mvid = mvidReservation.Content;
        StringHandle moduleName = metadata.GetOrAddString("tiny.mstat");
        metadata.AddModule(
            0,
            moduleName,
            mvidReservation.Handle,
            default,
            default);
        metadata.AddAssembly(
            moduleName,
            new Version(2, 2),
            default,
            default,
            0,
            AssemblyHashAlgorithm.None);
        metadata.AddTypeDefinition(
            TypeAttributes.NotPublic,
            default,
            metadata.GetOrAddString("<Module>"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));

        AssemblyReferenceHandle demoAssembly = metadata.AddAssemblyReference(
            metadata.GetOrAddString("Demo"),
            new Version(1, 0),
            default,
            default,
            0,
            default);
        TypeReferenceHandle boxType = metadata.AddTypeReference(
            demoAssembly,
            metadata.GetOrAddString("Demo"),
            metadata.GetOrAddString("Box`1"));

        var boxIntSignature = new BlobBuilder();
        boxIntSignature.WriteByte((byte)SignatureTypeCode.GenericTypeInstance);
        boxIntSignature.WriteByte((byte)SignatureTypeKind.Class);
        boxIntSignature.WriteCompressedInteger(CodedIndex.TypeDefOrRef(boxType));
        boxIntSignature.WriteCompressedInteger(1);
        boxIntSignature.WriteByte((byte)SignatureTypeCode.Int32);
        TypeSpecificationHandle boxInt = metadata.AddTypeSpecification(
            metadata.GetOrAddBlob(boxIntSignature));

        var methodSignature = new BlobBuilder();
        methodSignature.WriteByte(new SignatureHeader(
            SignatureKind.Method,
            SignatureCallingConvention.Default,
            SignatureAttributes.Generic).RawValue);
        methodSignature.WriteCompressedInteger(1);
        methodSignature.WriteCompressedInteger(1);
        methodSignature.WriteByte((byte)SignatureTypeCode.GenericMethodParameter);
        methodSignature.WriteCompressedInteger(0);
        methodSignature.WriteByte((byte)SignatureTypeCode.GenericMethodParameter);
        methodSignature.WriteCompressedInteger(0);
        MemberReferenceHandle method = metadata.AddMemberReference(
            boxInt,
            metadata.GetOrAddString("Echo"),
            metadata.GetOrAddBlob(methodSignature));

        var methodInstantiation = new BlobBuilder();
        new BlobEncoder(methodInstantiation).MethodSpecificationSignature(1);
        methodInstantiation.WriteByte((byte)SignatureTypeCode.Int32);
        EntityHandle methodEntity = method;
        if (methodEntity.Kind != HandleKind.MemberReference)
        {
            throw new InvalidOperationException(
                $"Synthetic member reference became {methodEntity.Kind}.");
        }

        MethodSpecificationHandle methodInt = metadata.AddMethodSpecification(
            methodEntity,
            metadata.GetOrAddBlob(methodInstantiation));

        var fieldSignature = new BlobBuilder();
        new BlobEncoder(fieldSignature).FieldSignature();
        fieldSignature.WriteByte((byte)SignatureTypeCode.Int32);
        MemberReferenceHandle field = metadata.AddMemberReference(
            boxInt,
            metadata.GetOrAddString("Value"),
            metadata.GetOrAddBlob(fieldSignature));

        var names = new BlobBuilder();
        int typeName = AppendName(names, "TYPE<&>");
        int boxIntName = AppendName(names, "BOX_INT");
        int boxIntAlternateName = AppendName(names, "BOX_INT_ALT");
        int methodName = AppendName(names, "METHOD_DEF");
        int methodIntName = AppendName(names, "METHOD_INT");
        int fieldName = AppendName(names, "FIELD_VALUE");
        int frozenName = AppendName(names, "FROZEN");
        int foldedName = AppendName(names, "FOLDED_TARGET");

        var methods = new InstructionEncoder(new BlobBuilder());
        WriteMethod(methods, method, 5, 0, 0, methodName);
        WriteMethod(methods, methodInt, 11, 2, 3, methodIntName);

        var types = new InstructionEncoder(new BlobBuilder());
        WriteType(types, boxType, 7, typeName);
        WriteType(types, boxInt, 13, boxIntName);
        WriteType(types, boxInt, 3, boxIntAlternateName);

        var blobs = new InstructionEncoder(new BlobBuilder());
        blobs.LoadString(metadata.GetOrAddUserString("Runtime blob"));
        blobs.LoadConstantI4(6);
        blobs.LoadString(metadata.GetOrAddUserString("FieldRvaData"));
        blobs.LoadConstantI4(4);

        var fields = new InstructionEncoder(new BlobBuilder());
        fields.OpCode(ILOpCode.Ldtoken);
        fields.Token(field);
        fields.LoadConstantI4(4);
        fields.LoadConstantI4(fieldName);

        var frozen = new InstructionEncoder(new BlobBuilder());
        frozen.OpCode(ILOpCode.Ldtoken);
        frozen.Token(boxInt);
        frozen.LoadConstantI4(9);
        frozen.LoadConstantI4(frozenName);
        frozen.OpCode(ILOpCode.Ldtoken);
        frozen.Token(boxInt);

        var resources = new InstructionEncoder(new BlobBuilder());
        resources.LoadConstantI4(MetadataTokens.GetToken(demoAssembly));
        resources.LoadString(metadata.GetOrAddUserString("demo.resources"));
        resources.LoadConstantI4(8);

        var deduplicated = new InstructionEncoder(new BlobBuilder());
        deduplicated.OpCode(ILOpCode.Ldtoken);
        deduplicated.Token(method);
        deduplicated.LoadConstantI4(1);
        deduplicated.OpCode(ILOpCode.Ldtoken);
        deduplicated.Token(methodInt);
        deduplicated.LoadConstantI4(foldedName);

        BlobHandle globalSignature = CreateGlobalSignature(metadata);
        AddGlobal(metadata, methodBodies, globalSignature, "Methods", methods);
        AddGlobal(metadata, methodBodies, globalSignature, "Types", types);
        AddGlobal(metadata, methodBodies, globalSignature, "Blobs", blobs);
        AddGlobal(metadata, methodBodies, globalSignature, "RvaFields", fields);
        AddGlobal(metadata, methodBodies, globalSignature, "FrozenObjects", frozen);
        AddGlobal(metadata, methodBodies, globalSignature, "ManifestResources", resources);
        AddGlobal(metadata, methodBodies, globalSignature, "DeduplicatedMethods", deduplicated);

        var peBuilder = new SyntheticPeBuilder(metadata, ilStream, names);
        var peBlob = new BlobBuilder();
        BlobContentId contentId = peBuilder.Serialize(peBlob);
        new BlobWriter(mvid).WriteGuid(contentId.Guid);
        using FileStream output = File.Create(path);
        peBlob.WriteContentTo(output);
    }

    private static int AppendName(BlobBuilder names, string value)
    {
        int offset = names.Count;
        names.WriteSerializedString(value);
        return offset;
    }

    private static void WriteMethod(
        InstructionEncoder encoder,
        EntityHandle method,
        int size,
        int gcInfo,
        int ehInfo,
        int nameOffset)
    {
        encoder.OpCode(ILOpCode.Ldtoken);
        encoder.Token(method);
        encoder.LoadConstantI4(size);
        encoder.LoadConstantI4(gcInfo);
        encoder.LoadConstantI4(ehInfo);
        encoder.LoadConstantI4(nameOffset);
    }

    private static void WriteType(
        InstructionEncoder encoder,
        EntityHandle type,
        int size,
        int nameOffset)
    {
        encoder.OpCode(ILOpCode.Ldtoken);
        encoder.Token(type);
        encoder.LoadConstantI4(size);
        encoder.LoadConstantI4(nameOffset);
    }

    private static BlobHandle CreateGlobalSignature(MetadataBuilder metadata)
    {
        var signature = new BlobBuilder();
        new BlobEncoder(signature).MethodSignature(
            SignatureCallingConvention.Default,
            0,
            isInstanceMethod: false);
        signature.WriteCompressedInteger(0);
        signature.WriteByte((byte)SignatureTypeCode.Void);
        return metadata.GetOrAddBlob(signature);
    }

    private static void AddGlobal(
        MetadataBuilder metadata,
        MethodBodyStreamEncoder methodBodies,
        BlobHandle signature,
        string name,
        InstructionEncoder body)
    {
        int bodyOffset = methodBodies.AddMethodBody(body, 1);
        metadata.AddMethodDefinition(
            MethodAttributes.Public | MethodAttributes.Static,
            MethodImplAttributes.IL,
            metadata.GetOrAddString(name),
            signature,
            bodyOffset,
            default);
    }

    private sealed class SyntheticPeBuilder : ManagedPEBuilder
    {
        private static readonly BlobContentId s_contentId =
            new(new Guid("12345678-1234-1234-1234-123456789abc"), 0x12345678);
        private readonly BlobBuilder _names;

        public SyntheticPeBuilder(
            MetadataBuilder metadata,
            BlobBuilder ilStream,
            BlobBuilder names)
            : base(
                PEHeaderBuilder.CreateLibraryHeader(),
                new MetadataRootBuilder(metadata),
                ilStream,
                deterministicIdProvider: static _ => s_contentId)
        {
            _names = names;
        }

        protected override ImmutableArray<Section> CreateSections() =>
            base.CreateSections().Add(new Section(".names", SectionCharacteristics.MemRead));

        protected override BlobBuilder SerializeSection(string name, SectionLocation location) =>
            name == ".names" ? _names : base.SerializeSection(name, location);
    }
}
