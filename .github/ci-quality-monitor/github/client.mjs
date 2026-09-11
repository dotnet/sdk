import { HttpClient } from "../http-client.mjs";

export async function getGitHubBranchHead(pipeline, branch, fetchImplementation = fetch) {
  const branchName = branch.replace(/^refs\/heads\//, "");
  const url = `https://api.github.com/repos/${pipeline.repository}/commits/${encodeURIComponent(branchName)}`;
  const commit = await new HttpClient(fetchImplementation).json(url);
  return {
    sha: commit.sha,
    committedAt: commit.commit?.committer?.date ?? commit.commit?.author?.date,
    url: commit.html_url
  };
}
