// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockFileChange : IFileChange, IFileChange2, IXunitSerializable
    {
        public MockFileChange()
        {

        }

        public MockFileChange(string source, string target, ChangeKind kind)
        {
            SourceRelativePath = source;
            TargetRelativePath = target;
            ChangeKind = kind;
        }

        public string SourceRelativePath { private set; get; }

        public string TargetRelativePath { private set; get; }

        public ChangeKind ChangeKind { private set; get; }

        public byte[] Contents => Array.Empty<byte>();

        public void Deserialize(IXunitSerializationInfo info)
        {
            SourceRelativePath = info.GetValue<string>("sourcePath");
            TargetRelativePath = info.GetValue<string>("targetPath");
            ChangeKind = (ChangeKind)info.GetValue<int>("kind");
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue("sourcePath", SourceRelativePath, typeof(string));
            info.AddValue("targetPath", TargetRelativePath, typeof(string));
            info.AddValue("kind", (int)ChangeKind, typeof(int));
        }

        public override string ToString()
        {
            return $"{SourceRelativePath}=>{TargetRelativePath}({ChangeKind})";
        }
    }
}
