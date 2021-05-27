// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using Microsoft.TemplateEngine.Abstractions;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockFileChange : IFileChange, IFileChange2, IXunitSerializable
    {
        private string? _sourcePath;
        private string? _targetPath;

        /// <summary>
        /// This is deserialization constructor only, do not use it. Requirement of <see cref="IXunitSerializable"/>.
        /// </summary>
        public MockFileChange()
        {
        }

        public MockFileChange(string source, string target, ChangeKind kind)
        {
            _sourcePath = source;
            _targetPath = target;
            ChangeKind = kind;
        }

        /// <summary>
        /// Should be set prior to use.
        /// </summary>
        public string SourceRelativePath
        {
            get
            {
                return _sourcePath ?? throw new Exception($"{nameof(SourceRelativePath)} was not set.");
            }

            set
            {
                _sourcePath = value;
            }
        }

        /// <summary>
        /// Should be set prior to use.
        /// </summary>
        public string TargetRelativePath
        {
            get
            {
                return _targetPath ?? throw new Exception($"{nameof(TargetRelativePath)} was not set.");
            }

            set
            {
                _targetPath = value;
            }
        }

        public ChangeKind ChangeKind { get; private set; }

        public byte[] Contents => Array.Empty<byte>();

        public void Deserialize(IXunitSerializationInfo info)
        {
            _sourcePath = info.GetValue<string>("sourcePath");
            _targetPath = info.GetValue<string>("targetPath");
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
