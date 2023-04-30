// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers.Credentials;

internal sealed class CredentialRetrievalException : Exception
{
	public CredentialRetrievalException(string registry, Exception innerException)
		: base(
            Resource.FormatString(nameof(Strings.FailedRetrievingCredentials), registry, innerException.Message),
            innerException)
	{ }
}
