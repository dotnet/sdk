// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Oci/DescriptorExtension.cs
// Copyright The ORAS Authors.
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using OrasProject.Oras.Exceptions;

namespace OrasProject.Oras.Oci
{
    internal static class DescriptorExtension
    {
        /// <summary>
        /// LimitSize throws SizeLimitExceededException if the size of desc exceeds the limit limitSize.
        /// </summary>
        /// <param name="desc"></param>
        /// <param name="limitSize"></param>
        /// <exception cref="SizeLimitExceededException"></exception>
        internal static void LimitSize(this Descriptor desc, long limitSize)
        {
            if (desc.Size > limitSize)
            {
                throw new SizeLimitExceededException($"content size {desc.Size} exceeds MaxMetadataBytes {limitSize}");
            }
        }
    }
}
