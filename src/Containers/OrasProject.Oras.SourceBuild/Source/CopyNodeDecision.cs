// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/CopyNodeDecision.cs
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

namespace OrasProject.Oras;

/// <summary>
/// Specifies the action to take during a copy operation.
/// </summary>
public enum CopyNodeDecision
{
    /// <summary>
    /// Continue with the copy operation.
    /// </summary>
    Continue = 0,

    /// <summary>
    /// Skip the current node and do not copy it.
    /// </summary>
    SkipNode = 1
}
