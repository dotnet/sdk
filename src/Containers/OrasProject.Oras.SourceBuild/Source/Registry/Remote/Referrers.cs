// Copied from https://github.com/jetersen/oras-dotnet/blob/b1cc2abbcdbfc6abee22fefbc5a62d166bcc5cca/src/OrasProject.Oras/Registry/Remote/Referrers.cs
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

using System.Collections.Generic;
using System.Linq;
using OrasProject.Oras.Content;
using OrasProject.Oras.Oci;

namespace OrasProject.Oras.Registry.Remote;

internal static class Referrers
{
    internal enum ReferrersState
    {
        Unknown = 0,
        Supported = 1,
        NotSupported = 2
    }

    internal record ReferrerChange(Descriptor Referrer, ReferrerOperation ReferrerOperation);

    internal const string ZeroDigest = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

    internal enum ReferrerOperation
    {
        Add,
        Delete,
    }

    internal static string BuildReferrersTag(Descriptor descriptor)
    {
        return Digest.Validate(descriptor.Digest).Replace(':', '-');
    }

    /// <summary>
    /// ApplyReferrerChanges applies the specified referrer change (either add or delete) to the existing list of referrers. 
    /// It updates the list based on the operation defined in the provided `referrerChange`. 
    /// If the referrer to be added or deleted already exists in the list, it is handled accordingly.
    /// </summary>
    /// <param name="oldReferrers"></param>
    /// <param name="referrerChange"></param>
    /// <returns>The updated referrers list, updateRequired</returns>
    internal static (IList<Descriptor>, bool) ApplyReferrerChanges(IList<Descriptor> oldReferrers, ReferrerChange referrerChange)
    {
        if (Descriptor.IsNullOrInvalid(referrerChange.Referrer))
        {
            return (oldReferrers, false);
        }

        // updatedReferrers is a list to store the updated referrers
        var updatedReferrers = new List<Descriptor>();
        // updatedReferrersSet is a HashSet to store unique referrers
        var updatedReferrersSet = new HashSet<BasicDescriptor>();

        var updateRequired = false;
        foreach (var oldReferrer in oldReferrers)
        {
            if (Descriptor.IsNullOrInvalid(oldReferrer))
            {
                // Skip any empty or null referrers
                updateRequired = true;
                continue;
            }

            var basicDesc = oldReferrer.BasicDescriptor;
            if (referrerChange.ReferrerOperation == ReferrerOperation.Delete && Equals(basicDesc, referrerChange.Referrer.BasicDescriptor))
            {
                updateRequired = true;
                continue;
            }

            if (updatedReferrersSet.Contains(basicDesc))
            {
                // Skip any duplicate referrers
                updateRequired = true;
                continue;
            }

            // Update the updatedReferrers list
            // Add referrer into the updatedReferrersSet
            updatedReferrers.Add(oldReferrer);
            updatedReferrersSet.Add(basicDesc);
        }

        if (referrerChange.ReferrerOperation == ReferrerOperation.Add)
        {
            var basicReferrerDesc = referrerChange.Referrer.BasicDescriptor;
            if (!updatedReferrersSet.Contains(basicReferrerDesc))
            {
                // Add the new referrer only when it has not already existed in the updatedReferrersSet
                updatedReferrers.Add(referrerChange.Referrer);
                updatedReferrersSet.Add(basicReferrerDesc);
                updateRequired = true;
            }
        }

        return (updatedReferrers, updateRequired);
    }

    /// <summary>
    /// IsReferrersFilterApplied checks if requstedFilter is in the applied filters list.
    /// </summary>
    /// <param name="appliedFilters"></param>
    /// <param name="requestedFilter"></param>
    /// <returns></returns>
    internal static bool IsReferrersFilterApplied(string appliedFilters, string requestedFilter)
    {
        if (string.IsNullOrEmpty(appliedFilters) || string.IsNullOrEmpty(requestedFilter))
        {
            return false;
        }

        var filters = appliedFilters.Split(",");
        foreach (var filter in filters)
        {
            if (filter == requestedFilter)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// FilterReferrers filters out a list of referrers based on the specified artifact type
    /// </summary>
    /// <param name="referrers"></param>
    /// <param name="artifactType"></param>
    /// <returns></returns>
    internal static IList<Descriptor> FilterReferrers(IList<Descriptor> referrers, string? artifactType)
    {
        return string.IsNullOrEmpty(artifactType) ? referrers : referrers.Where(referrer => referrer.ArtifactType == artifactType).ToList();
    }
}

