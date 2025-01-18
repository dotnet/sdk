// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

/// <summary>
/// A warning message that was sent during run.
/// </summary>
internal sealed record WarningMessage(string Text) : IProgressMessage;
