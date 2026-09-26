const FINGERPRINT_PATTERN = /\*\*Failure fingerprint:\*\*\s*`([^`]+)`/g;

export async function getRecentlyTrackedFingerprints(repository, token, fetchImplementation = fetch)
{
  if (!repository || !token) return new Set();
  const fingerprints = new Set();
  for (let page = 1; page <= 5; page++)
  {
    const url = `https://api.github.com/repos/${repository}/issues?state=open&sort=updated&direction=desc&per_page=100&page=${page}`;
    const response = await fetchImplementation(url, {
      headers: {
        Accept: "application/vnd.github+json",
        Authorization: `Bearer ${token}`,
        "User-Agent": "dotnet-sdk-ci-quality-monitor",
        "X-GitHub-Api-Version": "2022-11-28"
      }
    });
    if (!response.ok) throw new Error(`GET ${url} returned ${response.status} ${response.statusText}.`);
    const issues = await response.json();
    for (const issue of issues)
    {
      if (`${issue.title ?? ""}`.startsWith("[Archived")) continue;
      for (const match of `${issue.body ?? ""}`.matchAll(FINGERPRINT_PATTERN)) fingerprints.add(match[1]);
    }
    if (issues.length < 100) break;
  }
  return fingerprints;
}

export function suppressTrackedIssueCandidates(dossier, trackedFingerprints)
{
  for (const failure of dossier.failures)
  {
    failure.issueCandidates = (failure.issueCandidates ?? [])
      .filter(candidate => !trackedFingerprints.has(candidate.fingerprint));
  }
  return dossier;
}
