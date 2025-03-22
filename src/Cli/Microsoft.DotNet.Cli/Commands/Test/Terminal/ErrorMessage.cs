// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// An error message that was sent to output during the build.
/// </summary>
internal sealed record ErrorMessage(string Text) : IProgressMessage;
