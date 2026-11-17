// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Exceptions/AlreadyExistsException.cs
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
using System.IO;

namespace OrasProject.Oras.Exceptions;

/// <summary>
/// AlreadyExistsException is thrown when a resource already exists.
/// </summary>
public class AlreadyExistsException : IOException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AlreadyExistsException"/> class.
    /// </summary>
    public AlreadyExistsException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlreadyExistsException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public AlreadyExistsException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlreadyExistsException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public AlreadyExistsException(string? message, Exception? inner)
        : base(message, inner)
    {
    }
}
