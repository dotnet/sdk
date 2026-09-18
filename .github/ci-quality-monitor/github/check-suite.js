function resolveAzureBuildId(checks)
{
    const roots = checks.filter(check => check.name === "dotnet-sdk-public-ci");
    if (roots.length > 1)
    {
        throw new Error(`Expected at most one dotnet-sdk-public-ci root check, found ${roots.length}.`);
    }
    if (roots.length === 0) return null;

    const buildId = new URL(roots[0].details_url).searchParams.get("buildId");
    if (buildId !== null && !/^\d+$/.test(buildId))
    {
        throw new Error("The Azure root check URL did not contain a numeric buildId.");
    }
    return buildId;
}

module.exports = {resolveAzureBuildId};
