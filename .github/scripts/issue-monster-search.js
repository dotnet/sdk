const MAX_ISSUES_WITH_BODY_CONTEXT = 8;
const BODY_SNIPPET_MAX_LENGTH = 600;
const MAX_ISSUES_TO_INSPECT = 80;
const DETAIL_FETCH_CONCURRENCY = 5;

const EXCLUDED_LABELS = [
  "wontfix",
  "duplicate",
  "invalid",
  "question",
  "discussion",
  "needs-discussion",
  "blocked",
  "on-hold",
  "waiting-for-feedback",
  "needs-more-info",
  "no-bot",
  "area-security",
];

const PRIORITY_LABELS = [
  "good first issue",
  "help wanted",
  "cost:s",
  "bug",
  "documentation",
  "area-infrastructure",
  "test debt",
  "known build error",
  "enhancement",
  "fit-n-finish",
  "performance",
];

function setEmptyOutputs(core) {
  core.setOutput("issue_count", 0);
  core.setOutput("issue_numbers", "");
  core.setOutput("issue_list", "");
  core.setOutput("issue_context", "");
  core.setOutput("has_issues", "false");
}

async function hasRecentRateLimit({ github, core, owner, repo }) {
  core.info("Checking for recent rate-limited PRs...");
  const rateLimitCheckDate = new Date();
  rateLimitCheckDate.setHours(rateLimitCheckDate.getHours() - 1);
  const rateLimitCheckISO = `${rateLimitCheckDate.toISOString().split(".")[0]}Z`;
  const recentPRsQuery = `is:pr author:app/copilot-swe-agent created:>${rateLimitCheckISO} repo:${owner}/${repo}`;
  const recentPRsResponse = await github.rest.search.issuesAndPullRequests({
    q: recentPRsQuery,
    per_page: 10,
    sort: "created",
    order: "desc",
  });

  core.info(`Found ${recentPRsResponse.data.total_count} recent Copilot PRs to check for rate limiting`);

  for (const pr of recentPRsResponse.data.items) {
    try {
      const result = await github.graphql(
        `query($owner: String!, $repo: String!, $number: Int!) {
          repository(owner: $owner, name: $repo) {
            pullRequest(number: $number) {
              timelineItems(first: 50, itemTypes: [ISSUE_COMMENT]) {
                nodes {
                  ... on IssueComment {
                    body
                  }
                }
              }
            }
          }
        }`,
        { owner, repo, number: pr.number },
      );

      const comments = result?.repository?.pullRequest?.timelineItems?.nodes || [];
      const rateLimitPattern =
        /rate limit|API rate limit|secondary rate limit|abuse detection|\b429\b|too many requests/i;
      const matchingComment = comments.find((comment) => comment.body && rateLimitPattern.test(comment.body));
      if (matchingComment) {
        core.warning(`Rate limiting detected in PR #${pr.number}: ${matchingComment.body.substring(0, 200)}`);
        return true;
      }
    } catch (error) {
      core.warning(`Could not check PR #${pr.number} for rate limiting: ${error.message}`);
    }
  }

  core.info("No rate limiting detected. Proceeding with issue search.");
  return false;
}

async function fetchIssueDetails({
  github,
  core,
  owner,
  repo,
  issue,
  integrityFilteredIssues,
}) {
  let fullIssue;
  try {
    fullIssue = await github.rest.issues.get({
      owner,
      repo,
      issue_number: issue.number,
    });
  } catch (error) {
    const status = error.status || error.response?.status;
    const isIntegrityBlock = status === 403 || status === 451 || /\bintegrity\b/i.test(error.message || "");
    const errorSummary = (error.message || String(error)).slice(0, 120);
    if (isIntegrityBlock) {
      integrityFilteredIssues.push(issue.number);
      core.warning(
        `Skipping issue #${issue.number}: blocked by integrity policy (HTTP ${status || "unknown"}): ${errorSummary}`,
      );
    } else {
      core.warning(
        `Skipping issue #${issue.number}: could not fetch details (HTTP ${status || "unknown"}): ${errorSummary}`,
      );
    }
    return null;
  }

  let subIssuesCount = 0;
  let linkedPRs = [];
  try {
    const result = await github.graphql(
      `query($owner: String!, $repo: String!, $number: Int!) {
        repository(owner: $owner, name: $repo) {
          issue(number: $number) {
            subIssues {
              totalCount
            }
            timelineItems(first: 100, itemTypes: [CROSS_REFERENCED_EVENT]) {
              nodes {
                ... on CrossReferencedEvent {
                  source {
                    __typename
                    ... on PullRequest {
                      number
                      state
                      isDraft
                      author {
                        login
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }`,
      { owner, repo, number: issue.number },
    );

    subIssuesCount = result?.repository?.issue?.subIssues?.totalCount || 0;
    linkedPRs = (result?.repository?.issue?.timelineItems?.nodes || [])
      .filter((item) => item?.source?.__typename === "PullRequest")
      .map((item) => ({
        number: item.source.number,
        state: item.source.state,
        isDraft: item.source.isDraft,
        author: item.source.author?.login,
      }));
    core.info(`Issue #${issue.number} has ${linkedPRs.length} linked PR(s)`);
  } catch (error) {
    core.warning(`Could not check details for #${issue.number}: ${error.message}`);
  }

  return {
    ...fullIssue.data,
    subIssuesCount,
    linkedPRs,
  };
}

function scoreIssues(core, issues) {
  return issues
    .filter((issue) => {
      const copilotAssignees =
        issue.assignees?.filter(
          (assignee) => assignee.login === "copilot-swe-agent" || assignee.login?.includes("copilot"),
        ) || [];
      if (copilotAssignees.length > 0) {
        core.info(`Skipping #${issue.number}: already assigned to Copilot`);
        return false;
      }

      const issueLabels = issue.labels.map((label) => label.name.toLowerCase());
      if (issueLabels.some((label) => EXCLUDED_LABELS.includes(label))) {
        core.info(`Skipping #${issue.number}: has excluded label`);
        return false;
      }
      if (issue.subIssuesCount > 0) {
        core.info(`Skipping #${issue.number}: has ${issue.subIssuesCount} sub-issue(s)`);
        return false;
      }
      if (issue.linkedPRs?.length > 0) {
        core.info(`Skipping #${issue.number}: has ${issue.linkedPRs.length} linked PR(s)`);
        return false;
      }
      return true;
    })
    .map((issue) => {
      const issueLabels = issue.labels.map((label) => label.name.toLowerCase());
      let score = 0;
      if (issueLabels.includes("documentation")) score += 60;
      if (issueLabels.includes("bug")) score += 55;
      if (
        issueLabels.includes("area-infrastructure") ||
        issueLabels.includes("test debt") ||
        issueLabels.includes("known build error")
      ) {
        score += 50;
      }
      if (issueLabels.includes("cost:s")) score += 50;
      if (issueLabels.includes("good first issue") || issueLabels.includes("help wanted")) score += 40;
      if (issueLabels.includes("enhancement") || issueLabels.includes("fit-n-finish")) score += 40;
      if (issueLabels.includes("performance")) score += 30;
      if (issueLabels.some((label) => PRIORITY_LABELS.includes(label))) score += 10;

      const ageInDays = Math.floor((Date.now() - new Date(issue.created_at)) / (1000 * 60 * 60 * 24));
      score += Math.min(ageInDays / 10, 20);

      return {
        number: issue.number,
        title: issue.title,
        labels: issue.labels.map((label) => label.name),
        body: issue.body,
        score,
      };
    })
    .sort((left, right) => right.score - left.score);
}

module.exports = async function searchIssueMonsterCandidates({
  github,
  context,
  core,
  requestedIssueNumberInput,
}) {
  const { owner, repo } = context.repo;

  try {
    const requestedInput = requestedIssueNumberInput.trim();
    if (requestedInput && !/^[1-9]\d*$/.test(requestedInput)) {
      throw new Error(`Invalid issue number: ${requestedInput}`);
    }
    const requestedIssueNumber = requestedInput ? Number(requestedInput) : null;

    if (await hasRecentRateLimit({ github, core, owner, repo })) {
      core.warning("Rate limiting detected in recent PRs. Skipping issue assignment.");
      setEmptyOutputs(core);
      return;
    }

    let candidateIssues;
    if (requestedIssueNumber) {
      core.info(`Loading manually requested issue #${requestedIssueNumber}`);
      const requestedIssue = await github.rest.issues.get({
        owner,
        repo,
        issue_number: requestedIssueNumber,
      });
      const requestedLabels = requestedIssue.data.labels.map((label) => label.name.toLowerCase());
      const meetsBasicCriteria =
        !requestedIssue.data.pull_request &&
        requestedIssue.data.state === "open" &&
        requestedLabels.includes("cookie") &&
        !requestedLabels.some((label) => EXCLUDED_LABELS.includes(label));
      candidateIssues = meetsBasicCriteria ? [requestedIssue.data] : [];
      if (!meetsBasicCriteria) {
        core.info(`Skipping manually requested issue #${requestedIssueNumber}: it does not meet the criteria`);
      }
    } else {
      const query = `is:issue is:open repo:${owner}/${repo} label:cookie -label:"${EXCLUDED_LABELS.join(
        '" -label:"',
      )}"`;
      core.info(`Searching: ${query}`);
      const response = await github.rest.search.issuesAndPullRequests({
        q: query,
        per_page: 100,
        sort: "created",
        order: "desc",
      });
      core.info(`Found ${response.data.total_count} total issues matching basic criteria`);
      candidateIssues = response.data.items;
    }

    const integrityFilteredIssues = [];
    const issuesToInspect = candidateIssues.slice(0, MAX_ISSUES_TO_INSPECT);
    if (candidateIssues.length > issuesToInspect.length) {
      core.info(`Inspecting the ${issuesToInspect.length} most recent of ${candidateIssues.length} matching issues`);
    }

    const detailedIssues = [];
    for (let start = 0; start < issuesToInspect.length; start += DETAIL_FETCH_CONCURRENCY) {
      const batch = issuesToInspect.slice(start, start + DETAIL_FETCH_CONCURRENCY);
      const results = await Promise.all(
        batch.map((issue) =>
          fetchIssueDetails({
            github,
            core,
            owner,
            repo,
            issue,
            integrityFilteredIssues,
          }),
        ),
      );
      detailedIssues.push(...results);
    }

    if (integrityFilteredIssues.length > 0) {
      core.warning(
        `Integrity filter diagnostic: ${integrityFilteredIssues.length} issue(s) skipped: #${integrityFilteredIssues.join(
          ", #",
        )}`,
      );
    }

    const scoredIssues = scoreIssues(core, detailedIssues.filter(Boolean));
    const issueList = scoredIssues
      .map((issue) => {
        const labels = issue.labels.length > 0 ? ` [${issue.labels.join(", ")}]` : "";
        return `#${issue.number}: ${issue.title}${labels} (score: ${issue.score.toFixed(1)})`;
      })
      .join("\n");
    const issueContext = scoredIssues
      .slice(0, MAX_ISSUES_WITH_BODY_CONTEXT)
      .map((issue) => {
        const body = (issue.body || "").replace(/\s+/g, " ").trim();
        const snippet =
          body.length > BODY_SNIPPET_MAX_LENGTH ? `${body.slice(0, BODY_SNIPPET_MAX_LENGTH)}…` : body;
        const labels = issue.labels.length > 0 ? issue.labels.join(", ") : "none";
        return `#${issue.number} | score=${issue.score.toFixed(1)} | labels=${labels}\nTitle: ${
          issue.title
        }\nBody: ${snippet || "(no body)"}`;
      })
      .join("\n\n---\n\n");

    core.info(`Total candidate issues: ${scoredIssues.length}`);
    if (scoredIssues.length > 0) {
      core.info(`Top candidates:\n${issueList.split("\n").slice(0, 10).join("\n")}`);
    } else {
      core.info("🍽️ No suitable candidate issues - the plate is empty!");
    }

    core.setOutput("issue_count", scoredIssues.length);
    core.setOutput(
      "issue_numbers",
      scoredIssues.map((issue) => issue.number).join(","),
    );
    core.setOutput("issue_list", issueList);
    core.setOutput("issue_context", issueContext);
    core.setOutput("has_issues", scoredIssues.length > 0 ? "true" : "false");
  } catch (error) {
    core.error(`Error searching for issues: ${error.message}`);
    setEmptyOutputs(core);
  }
};
