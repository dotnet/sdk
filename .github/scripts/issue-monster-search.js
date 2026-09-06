// Candidate search and output limits
const MAX_ISSUES_WITH_BODY_CONTEXT = 8;
const BODY_SNIPPET_MAX_LENGTH = 600;
const MAX_ISSUES_TO_INSPECT = 80;
const DETAIL_FETCH_CONCURRENCY = 5;
const CANDIDATE_SEARCH_PAGE_SIZE = 100;
const TOP_CANDIDATE_LOG_COUNT = 10;

// Issue and assignee identifiers
const WORK_QUEUE_LABEL = "cookie";
const OPEN_ISSUE_STATE = "open";
const COPILOT_LOGIN_FRAGMENT = "copilot";
const COPILOT_ASSIGNEE_LOGIN = `${COPILOT_LOGIN_FRAGMENT}-swe-agent`;
const COPILOT_PR_AUTHOR = `app/${COPILOT_ASSIGNEE_LOGIN}`;

// Rate-limit detection
const RECENT_PR_SEARCH_PAGE_SIZE = 10;
const RATE_LIMIT_LOOKBACK_HOURS = 1;
const RATE_LIMIT_COMMENT_COUNT = 50;
const RATE_LIMIT_COMMENT_PREVIEW_LENGTH = 200;
const RATE_LIMIT_PATTERN =
    /rate limit|API rate limit|secondary rate limit|abuse detection|\b429\b|too many requests/i;

// Issue detail retrieval and error reporting
const ISSUE_RELATION_COUNT = 100;
const ERROR_SUMMARY_LENGTH = 120;
const UNKNOWN_HTTP_STATUS = "unknown";
const INTEGRITY_BLOCK_STATUSES = new Set([403, 451]);

// Scoring
const DAYS_PER_AGE_SCORE_POINT = 10;
const MAX_AGE_SCORE = 20;
const PRIORITY_LABEL_BONUS = 10;
const MILLISECONDS_PER_HOUR = 60 * 60 * 1000;
const MILLISECONDS_PER_DAY = 24 * MILLISECONDS_PER_HOUR;

// Label policy
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

const LABEL_SCORE_RULES = [
    {labels: ["documentation"], score: 60},
    {labels: ["bug"], score: 55},
    {labels: ["area-infrastructure", "test debt", "known build error"], score: 50},
    {labels: ["cost:s"], score: 50},
    {labels: ["good first issue", "help wanted"], score: 40},
    {labels: ["enhancement", "fit-n-finish"], score: 40},
    {labels: ["performance"], score: 30},
];

// GraphQL queries
const RATE_LIMIT_COMMENTS_QUERY = `query($owner: String!, $repo: String!, $number: Int!) {
    repository(owner: $owner, name: $repo) {
        pullRequest(number: $number) {
            timelineItems(first: ${RATE_LIMIT_COMMENT_COUNT}, itemTypes: [ISSUE_COMMENT]) {
                nodes {
                    ... on IssueComment {
                        body
                    }
                }
            }
        }
    }
}`;

const ISSUE_RELATIONS_QUERY = `query($owner: String!, $repo: String!, $number: Int!) {
    repository(owner: $owner, name: $repo) {
        issue(number: $number) {
            subIssues {
                totalCount
            }
            timelineItems(first: ${ISSUE_RELATION_COUNT}, itemTypes: [CROSS_REFERENCED_EVENT]) {
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
}`;

function setOutputs(core, {issueCount, issueNumbers, issueList, issueContext})
{
    core.setOutput("issue_count", issueCount);
    core.setOutput("issue_numbers", issueNumbers);
    core.setOutput("issue_list", issueList);
    core.setOutput("issue_context", issueContext);
    core.setOutput("has_issues", issueCount > 0 ? "true" : "false");
}

function setEmptyOutputs(core)
{
    setOutputs(core, {
        issueCount: 0,
        issueNumbers: "",
        issueList: "",
        issueContext: "",
    });
}

async function hasRecentRateLimit({github, core, owner, repo})
{
    core.info("Checking for recent rate-limited PRs...");
    const rateLimitCheckDate = new Date(Date.now() - RATE_LIMIT_LOOKBACK_HOURS * MILLISECONDS_PER_HOUR);
    const rateLimitCheckISO = `${rateLimitCheckDate.toISOString().split(".")[0]}Z`;
    const recentPRsQuery = `is:pr author:${COPILOT_PR_AUTHOR} created:>${rateLimitCheckISO} repo:${owner}/${repo}`;
    const recentPRsResponse = await github.rest.search.issuesAndPullRequests({
        q: recentPRsQuery,
        per_page: RECENT_PR_SEARCH_PAGE_SIZE,
        sort: "created",
        order: "desc",
    });

    core.info(`Found ${recentPRsResponse.data.total_count} recent Copilot PRs to check for rate limiting`);

    for (const pr of recentPRsResponse.data.items)
    {
        try
        {
            const result = await github.graphql(
                RATE_LIMIT_COMMENTS_QUERY,
                {owner, repo, number: pr.number},
            );

            const comments = result?.repository?.pullRequest?.timelineItems?.nodes ?? [];
            const matchingComment = comments.find((comment) => comment.body && RATE_LIMIT_PATTERN.test(comment.body));
            if (matchingComment)
            {
                core.warning(
                    `Rate limiting detected in PR #${pr.number}: ${matchingComment.body.substring(0, RATE_LIMIT_COMMENT_PREVIEW_LENGTH)}`,
                );
                return true;
            }
        } catch (error)
        {
            core.warning(`Could not check PR #${pr.number} for rate limiting: ${error.message}`);
        }
    }

    core.info("No rate limiting detected. Proceeding with issue search.");
    return false;
}

async function fetchFullIssue({
    github,
    core,
    owner,
    repo,
    issue,
    integrityFilteredIssues,
})
{
    try
    {
        return await github.rest.issues.get({
            owner,
            repo,
            issue_number: issue.number,
        });
    } catch (error)
    {
        const status = error.status ?? error.response?.status;
        const displayedStatus = status ?? UNKNOWN_HTTP_STATUS;
        const isIntegrityBlock = INTEGRITY_BLOCK_STATUSES.has(status) || /\bintegrity\b/i.test(error.message ?? "");
        const errorSummary = (error.message || String(error)).slice(0, ERROR_SUMMARY_LENGTH);
        if (isIntegrityBlock)
        {
            integrityFilteredIssues.push(issue.number);
            core.warning(
                `Skipping issue #${issue.number}: blocked by integrity policy (HTTP ${displayedStatus}): ${errorSummary}`,
            );
        } else
        {
            core.warning(
                `Skipping issue #${issue.number}: could not fetch details (HTTP ${displayedStatus}): ${errorSummary}`,
            );
        }
        return null;
    }
}

async function fetchIssueRelations({github, core, owner, repo, issueNumber})
{
    let subIssuesCount = 0;
    let linkedPRs = [];
    try
    {
        const result = await github.graphql(
            ISSUE_RELATIONS_QUERY,
            {owner, repo, number: issueNumber},
        );

        subIssuesCount = result?.repository?.issue?.subIssues?.totalCount ?? 0;
        linkedPRs = (result?.repository?.issue?.timelineItems?.nodes ?? [])
            .filter((item) => item?.source?.__typename === "PullRequest")
            .map((item) => ({
                number: item.source.number,
                state: item.source.state,
                isDraft: item.source.isDraft,
                author: item.source.author?.login,
            }));
        core.info(`Issue #${issueNumber} has ${linkedPRs.length} linked PR(s)`);
    } catch (error)
    {
        core.warning(`Could not check details for #${issueNumber}: ${error.message}`);
    }

    return {subIssuesCount, linkedPRs};
}

async function fetchIssueDetails(options)
{
    const fullIssue = await fetchFullIssue(options);
    if (!fullIssue)
    {
        return null;
    }

    const {github, core, owner, repo, issue} = options;
    const relations = await fetchIssueRelations({github, core, owner, repo, issueNumber: issue.number});
    return {
        ...fullIssue.data,
        ...relations,
    };
}

function getNormalizedLabelSet(issue)
{
    return new Set(issue.labels.map((label) => label.name.toLowerCase()));
}

function hasExcludedLabel(issueLabels)
{
    return EXCLUDED_LABELS.some((label) => issueLabels.has(label));
}

function isCopilotAssignee(assignee)
{
    const login = assignee.login ?? "";
    return login === COPILOT_ASSIGNEE_LOGIN || login.includes(COPILOT_LOGIN_FRAGMENT);
}

function isEligibleIssue(core, issue, issueLabels)
{
    if ((issue.assignees ?? []).some(isCopilotAssignee))
    {
        core.info(`Skipping #${issue.number}: already assigned to Copilot`);
        return false;
    }
    if (hasExcludedLabel(issueLabels))
    {
        core.info(`Skipping #${issue.number}: has excluded label`);
        return false;
    }
    if (issue.subIssuesCount > 0)
    {
        core.info(`Skipping #${issue.number}: has ${issue.subIssuesCount} sub-issue(s)`);
        return false;
    }
    if (issue.linkedPRs.length > 0)
    {
        core.info(`Skipping #${issue.number}: has ${issue.linkedPRs.length} linked PR(s)`);
        return false;
    }
    return true;
}

function calculateIssueScore(issue, issueLabels)
{
    let score = 0;
    let hasPriorityLabel = false;
    for (const rule of LABEL_SCORE_RULES)
    {
        if (rule.labels.some((label) => issueLabels.has(label)))
        {
            score += rule.score;
            hasPriorityLabel = true;
        }
    }
    if (hasPriorityLabel)
    {
        score += PRIORITY_LABEL_BONUS;
    }

    const ageInDays = Math.floor((Date.now() - new Date(issue.created_at)) / MILLISECONDS_PER_DAY);
    return score + Math.min(ageInDays / DAYS_PER_AGE_SCORE_POINT, MAX_AGE_SCORE);
}

function scoreIssues(core, issues)
{
    const scoredIssues = [];
    for (const issue of issues)
    {
        const issueLabels = getNormalizedLabelSet(issue);
        if (isEligibleIssue(core, issue, issueLabels))
        {
            scoredIssues.push({
                number: issue.number,
                title: issue.title,
                labels: issue.labels.map((label) => label.name),
                body: issue.body,
                score: calculateIssueScore(issue, issueLabels),
            });
        }
    }

    return scoredIssues.sort((left, right) => right.score - left.score);
}

function parseRequestedIssueNumber(requestedIssueNumberInput)
{
    const requestedInput = requestedIssueNumberInput.trim();
    if (requestedInput && !/^[1-9]\d*$/.test(requestedInput))
    {
        throw new Error(`Invalid issue number: ${requestedInput}`);
    }
    return requestedInput ? Number(requestedInput) : null;
}

async function loadRequestedIssue({github, core, owner, repo, requestedIssueNumber})
{
    core.info(`Loading manually requested issue #${requestedIssueNumber}`);
    const response = await github.rest.issues.get({
        owner,
        repo,
        issue_number: requestedIssueNumber,
    });
    const issue = response.data;
    const issueLabels = getNormalizedLabelSet(issue);
    const meetsBasicCriteria =
        !issue.pull_request &&
        issue.state === OPEN_ISSUE_STATE &&
        issueLabels.has(WORK_QUEUE_LABEL) &&
        !hasExcludedLabel(issueLabels);

    if (!meetsBasicCriteria)
    {
        core.info(`Skipping manually requested issue #${requestedIssueNumber}: it does not meet the criteria`);
        return [];
    }
    return [issue];
}

async function searchCandidateIssues({github, core, owner, repo})
{
    const query = `is:issue is:${OPEN_ISSUE_STATE} repo:${owner}/${repo} label:${WORK_QUEUE_LABEL} -label:"${EXCLUDED_LABELS.join(
        '" -label:"',
    )}"`;
    core.info(`Searching: ${query}`);
    const response = await github.rest.search.issuesAndPullRequests({
        q: query,
        per_page: CANDIDATE_SEARCH_PAGE_SIZE,
        sort: "created",
        order: "desc",
    });
    core.info(`Found ${response.data.total_count} total issues matching basic criteria`);
    return response.data.items;
}

async function findCandidateIssues(options)
{
    return options.requestedIssueNumber
        ? loadRequestedIssue(options)
        : searchCandidateIssues(options);
}

async function fetchCandidateDetails({github, core, owner, repo, candidateIssues})
{
    const issuesToInspect = candidateIssues.slice(0, MAX_ISSUES_TO_INSPECT);
    if (candidateIssues.length > issuesToInspect.length)
    {
        core.info(`Inspecting the ${issuesToInspect.length} most recent of ${candidateIssues.length} matching issues`);
    }

    const integrityFilteredIssues = [];
    const detailedIssues = [];
    for (let start = 0; start < issuesToInspect.length; start += DETAIL_FETCH_CONCURRENCY)
    {
        const batch = issuesToInspect.slice(start, start + DETAIL_FETCH_CONCURRENCY);
        const results = await Promise.all(
            batch.map((issue) =>
                fetchIssueDetails({github, core, owner, repo, issue, integrityFilteredIssues}),
            ),
        );
        detailedIssues.push(...results);
    }

    if (integrityFilteredIssues.length > 0)
    {
        core.warning(
            `Integrity filter diagnostic: ${integrityFilteredIssues.length} issue(s) skipped: #${integrityFilteredIssues.join(
                ", #",
            )}`,
        );
    }
    return detailedIssues.filter(Boolean);
}

function createIssueList(scoredIssues)
{
    return scoredIssues
        .map((issue) =>
        {
            const labels = issue.labels.length > 0 ? ` [${issue.labels.join(", ")}]` : "";
            return `#${issue.number}: ${issue.title}${labels} (score: ${issue.score.toFixed(1)})`;
        })
        .join("\n");
}

function createIssueContext(scoredIssues)
{
    return scoredIssues
        .slice(0, MAX_ISSUES_WITH_BODY_CONTEXT)
        .map((issue) =>
        {
            const body = (issue.body ?? "").replace(/\s+/g, " ").trim();
            const snippet =
                body.length > BODY_SNIPPET_MAX_LENGTH ? `${body.slice(0, BODY_SNIPPET_MAX_LENGTH)}…` : body;
            const labels = issue.labels.length > 0 ? issue.labels.join(", ") : "none";
            return `#${issue.number} | score=${issue.score.toFixed(1)} | labels=${labels}\nTitle: ${issue.title}\nBody: ${snippet || "(no body)"}`;
        })
        .join("\n\n---\n\n");
}

function createCandidateOutputs(scoredIssues)
{
    return {
        issueCount: scoredIssues.length,
        issueNumbers: scoredIssues.map((issue) => issue.number).join(","),
        issueList: createIssueList(scoredIssues),
        issueContext: createIssueContext(scoredIssues),
    };
}

function logCandidateSummary(core, {issueCount, issueList})
{
    core.info(`Total candidate issues: ${issueCount}`);
    if (issueCount > 0)
    {
        core.info(`Top candidates:\n${issueList.split("\n").slice(0, TOP_CANDIDATE_LOG_COUNT).join("\n")}`);
    }
    else
    {
        core.info("🍽️ No suitable candidate issues - the plate is empty!");
    }
}

module.exports = async function searchIssueMonsterCandidates({
    github,
    context,
    core,
    requestedIssueNumberInput,
})
{
    const {owner, repo} = context.repo;

    try
    {
        const requestedIssueNumber = parseRequestedIssueNumber(requestedIssueNumberInput);
        if (await hasRecentRateLimit({github, core, owner, repo}))
        {
            core.warning("Rate limiting detected in recent PRs. Skipping issue assignment.");
            setEmptyOutputs(core);
            return;
        }

        const candidateIssues = await findCandidateIssues({github, core, owner, repo, requestedIssueNumber});
        const detailedIssues = await fetchCandidateDetails({github, core, owner, repo, candidateIssues});
        const outputs = createCandidateOutputs(scoreIssues(core, detailedIssues));
        logCandidateSummary(core, outputs);
        setOutputs(core, outputs);
    } catch (error)
    {
        core.error(`Error searching for issues: ${error.message}`);
        setEmptyOutputs(core);
    }
};
