// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Exceptions/MissingArtifactTypeException.cs
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

using System;

namespace OrasProject.Oras.Exceptions;

/// <summary>
/// MissingArtifactTypeException is thrown when artifactType is not found in manifest.
/// </summary>
public class MissingArtifactTypeException : FormatException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissingArtifactTypeException"/> class.
    /// </summary>
    public MissingArtifactTypeException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingArtifactTypeException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public MissingArtifactTypeException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingArtifactTypeException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public MissingArtifactTypeException(string? message, Exception? inner)
        : base(message, inner)
    {
    }
}
