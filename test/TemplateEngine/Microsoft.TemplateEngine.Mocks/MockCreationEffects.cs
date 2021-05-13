// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockCreationEffects : ICreationEffects, ICreationEffects2, IXunitSerializable
    {
        private string[] _primaryOutputs = Array.Empty<string>();

        private MockFileChange[] _mockFileChanges = Array.Empty<MockFileChange>();

        private string[] _absentFiles = Array.Empty<string>();

        public MockCreationEffects()
        {
        }

        public IReadOnlyList<IFileChange> FileChanges => _mockFileChanges;

        public ICreationResult CreationResult
        {
            get
            {
                return new MockCreationResult(primaryOutputs: _primaryOutputs.Select(p => new MockCreationPath(p)).ToList());
            }
        }

        IReadOnlyList<IFileChange2> ICreationEffects2.FileChanges => _mockFileChanges;

        public IEnumerable<string> AbsentFiles => _absentFiles.Where(path => !path.EndsWith("/") && !path.EndsWith("\\"));

        public IEnumerable<string> AbsentDirectories => _absentFiles.Where(path => path.EndsWith("/") || path.EndsWith("\\"));

        public MockCreationEffects WithPrimaryOutputs(params string[] primaryOutputs)
        {
            _primaryOutputs = _primaryOutputs.Concat(primaryOutputs).ToArray();
            return this;
        }

        public MockCreationEffects WithFileChange(params MockFileChange[] fileChanges)
        {
            _mockFileChanges = _mockFileChanges.Concat(fileChanges).ToArray();
            return this;
        }

        public MockCreationEffects Without(params string[] files)
        {
            _absentFiles = _absentFiles.Concat(files).ToArray();
            return this;
        }

        public void Deserialize(IXunitSerializationInfo info)
        {
            _primaryOutputs = info.GetValue<string[]>("primaryOutputs");
            _mockFileChanges = info.GetValue<MockFileChange[]>("fileChanges");
            _absentFiles = info.GetValue<string[]>("absentFiles");
        }

        public void Serialize(IXunitSerializationInfo info)
        {
            info.AddValue("primaryOutputs", _primaryOutputs, typeof(string[]));
            info.AddValue("fileChanges", _mockFileChanges, typeof(MockFileChange[]));
            info.AddValue("absentFiles", _absentFiles, typeof(string[]));
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            _ = sb.Append("Primary outputs:" + string.Join("|", _primaryOutputs) + ";");
            _ = sb.Append("File changes:" + string.Join<MockFileChange>("|", _mockFileChanges) + ";");

            return sb.ToString();
        }
    }
}
