﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Test;

internal sealed record TestSessionEvent(byte? SessionType, string? SessionUid, string? ExecutionId) : IRequest;
