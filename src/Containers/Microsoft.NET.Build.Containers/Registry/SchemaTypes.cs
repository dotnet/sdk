// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.NET.Build.Containers;

internal class SchemaTypes
{
    internal const string DockerContainerV1 = "application/vnd.docker.container.image.v1+json";
    internal const string DockerManifestListV2 = "application/vnd.docker.distribution.manifest.list.v2+json";
    internal const string DockerManifestV2 = "application/vnd.docker.distribution.manifest.v2+json";

    internal const string OciManifestV1 = "application/vnd.oci.image.manifest.v1+json"; // https://containers.gitbook.io/build-containers-the-hard-way/#registry-format-oci-image-manifest
    internal const string OciImageIndexV1 = "application/vnd.oci.image.index.v1+json";
    internal const string OciImageConfigV1 = "application/vnd.oci.image.config.v1+json";

    internal const string DockerLayerGzip = "application/vnd.docker.image.rootfs.diff.tar.gzip";
    internal const string OciLayerGzipV1 = "application/vnd.oci.image.layer.v1.tar+gzip";
}
