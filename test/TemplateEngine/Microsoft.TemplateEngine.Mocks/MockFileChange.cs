// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockFileChange : IFileChange, IFileChange2
    {
        private string? _sourcePath;
        private string? _targetPath;

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
            get => _sourcePath ?? throw new Exception($"{nameof(SourceRelativePath)} was not set.");

            set => _sourcePath = value;
        }

        /// <summary>
        /// Should be set prior to use.
        /// </summary>
        public string TargetRelativePath
        {
            get => _targetPath ?? throw new Exception($"{nameof(TargetRelativePath)} was not set.");

            set => _targetPath = value;
        }

        public ChangeKind ChangeKind { get; private set; }

        public byte[] Contents => [];

        public override string ToString()
        {
            return $"{SourceRelativePath}=>{TargetRelativePath}({ChangeKind})";
        }
    }
}
