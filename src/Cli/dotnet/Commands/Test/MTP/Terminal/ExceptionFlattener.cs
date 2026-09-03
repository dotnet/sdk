// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

internal sealed record FlatException(string? ErrorMessage, string? ErrorType, string? StackTrace);
