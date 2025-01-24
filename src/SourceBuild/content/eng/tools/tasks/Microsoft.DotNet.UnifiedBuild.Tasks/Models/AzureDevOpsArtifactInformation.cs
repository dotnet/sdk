// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.UnifiedBuild.Tasks;

public record AzureDevOpsArtifactInformation(int Id, string Name, string Source, AzdoArtifactResources Resource);
public record AzdoArtifactResources(string Type, string Data, AzdoArtifactProperties Properties, string Url, string DownloadUrl);
public record AzdoArtifactProperties(string RootId, string Artifactsize, string HashType, string DomainId);