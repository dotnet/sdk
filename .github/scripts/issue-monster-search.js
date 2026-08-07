const { owner, repo } = context.repo;
const MAX_ISSUES_WITH_BODY_CONTEXT = 8;
const BODY_SNIPPET_MAX_LENGTH = 600;
// Cap how many issues we fetch full details for, and how many detail
// requests run at once, so a scheduled run does not fan out up to 100
// concurrent REST + GraphQL calls and trip GitHub secondary rate limiting.
const MAX_ISSUES_TO_INSPECT = 80;
const DETAIL_FETCH_CONCURRENCY = 5;
try {
  const requestedIssueNumberInput = `${{ github.event.inputs.issue_number || '' }}`.trim();
  if (requestedIssueNumberInput && !/^[1-9]\d*$/.test(requestedIssueNumberInput)) {
    throw new Error(`Invalid issue number: ${requestedIssueNumberInput}`);
  }
  const requestedIssueNumber = requestedIssueNumberInput ? Number(requestedIssueNumberInput) : null;

  // Check for recent rate-limited PRs to avoid scheduling more work during rate limiting
  core.info('Checking for recent rate-limited PRs...');
  const rateLimitCheckDate = new Date();
  rateLimitCheckDate.setHours(rateLimitCheckDate.getHours() - 1); // Check last hour
  // Format as YYYY-MM-DDTHH:MM:SS for GitHub search API
  const rateLimitCheckISO = rateLimitCheckDate.toISOString().split('.')[0] + 'Z';

  const recentPRsQuery = `is:pr author:app/copilot-swe-agent created:>${rateLimitCheckISO} repo:${owner}/${repo}`;
  const recentPRsResponse = await github.rest.search.issuesAndPullRequests({
    q: recentPRsQuery,
    per_page: 10,
    sort: 'created',
    order: 'desc'
  });

  core.info(`Found ${recentPRsResponse.data.total_count} recent Copilot PRs to check for rate limiting`);

  // Check if any recent PRs have rate limit indicators
  let rateLimitDetected = false;
  for (const pr of recentPRsResponse.data.items) {
    try {
      const prTimelineQuery = `
        query($owner: String!, $repo: String!, $number: Int!) {
          repository(owner: $owner, name: $repo) {
            pullRequest(number: $number) {
              timelineItems(first: 50, itemTypes: [ISSUE_COMMENT]) {
                nodes {
                  __typename
                  ... on IssueComment {
                    body
                    createdAt
                  }
                }
              }
            }
          }
        }
      `;

      const prTimelineResult = await github.graphql(prTimelineQuery, {
        owner,
        repo,
        number: pr.number
      });

      const comments = prTimelineResult?.repository?.pullRequest?.timelineItems?.nodes || [];
      const rateLimitPattern = /rate limit|API rate limit|secondary rate limit|abuse detection|\b429\b|too many requests/i;

      for (const comment of comments) {
        if (comment.body && rateLimitPattern.test(comment.body)) {
          core.warning(`Rate limiting detected in PR #${pr.number}: ${comment.body.substring(0, 200)}`);
          rateLimitDetected = true;
          break;
        }
      }

      if (rateLimitDetected) break;
    } catch (error) {
      core.warning(`Could not check PR #${pr.number} for rate limiting: ${error.message}`);
    }
  }

  if (rateLimitDetected) {
    core.warning('🛑 Rate limiting detected in recent PRs. Skipping issue assignment to prevent further rate limit issues.');
    core.setOutput('issue_count', 0);
    core.setOutput('issue_numbers', '');
    core.setOutput('issue_list', '');
    core.setOutput('issue_context', '');
    core.setOutput('has_issues', 'false');
    return;
  }

  core.info('✓ No rate limiting detected. Proceeding with issue search.');

  // Labels that indicate an issue should NOT be auto-assigned
  const excludeLabels = [
    'wontfix',
    'duplicate',
    'invalid',
    'question',
    'discussion',
    'needs-discussion',
    'blocked',
    'on-hold',
    'waiting-for-feedback',
    'needs-more-info',
    'no-bot',
    // Security triage marker. When the orchestrator refuses a security-sensitive
    // issue it removes 'cookie' and adds 'Area-Security', which retires the issue
    // from all future candidate searches (enforced here at the fetch step so
    // Area-Security issues are never even considered).
    'area-security'
  ];

  // Labels that indicate an issue is a GOOD candidate for auto-assignment
  const priorityLabels = [
    'good first issue',
    'help wanted',
    'cost:s',
    'bug',
    'documentation',
    'area-infrastructure',
    'test debt',
    'known build error',
    'enhancement',
    'fit-n-finish',
    'performance'
  ];

  let candidateIssues;
  if (requestedIssueNumber) {
    core.info(`Loading manually requested issue #${requestedIssueNumber}`);
    const requestedIssue = await github.rest.issues.get({
      owner,
      repo,
      issue_number: requestedIssueNumber
    });
    const requestedLabels = requestedIssue.data.labels.map(label => label.name.toLowerCase());
    const meetsBasicCriteria =
      !requestedIssue.data.pull_request &&
      requestedIssue.data.state === 'open' &&
      requestedLabels.includes('cookie') &&
      !requestedLabels.some(label => excludeLabels.includes(label));

    if (meetsBasicCriteria) {
      candidateIssues = [requestedIssue.data];
    } else {
      core.info(`Skipping manually requested issue #${requestedIssueNumber}: it does not meet the open cookie issue criteria`);
      candidateIssues = [];
    }
  } else {
    // Search for open issues with "cookie" label and without excluded labels.
    // The "cookie" label indicates issues that are approved work queue items from automated workflows.
    const query = `is:issue is:open repo:${owner}/${repo} label:cookie -label:"${excludeLabels.join('" -label:"')}"`;
    core.info(`Searching: ${query}`);
    const response = await github.rest.search.issuesAndPullRequests({
      q: query,
      per_page: 100,
      sort: 'created',
      order: 'desc'
    });
    core.info(`Found ${response.data.total_count} total issues matching basic criteria`);
    candidateIssues = response.data.items;
  }

  // Fetch full details for each issue to get labels, sub-issues, and linked PRs
  // Track integrity-filtered issues to emit a diagnostic summary
  const integrityFilteredIssues = [];
  const fetchIssueDetails = async (issue) => {
      // Fetch full issue details — some issues may be blocked by integrity policy
      let fullIssue;
      try {
        fullIssue = await github.rest.issues.get({
          owner,
          repo,
          issue_number: issue.number
        });
      } catch (fetchError) {
        // Integrity-filtered issues (403/451) or other transient errors should be
        // skipped individually rather than failing the entire batch
        const status = fetchError.status || fetchError.response?.status;
        // 403 = Forbidden (integrity policy), 451 = Unavailable For Legal Reasons
        const isIntegrityBlock = status === 403 || status === 451 ||
          /\bintegrity\b/i.test(fetchError.message || '');
        const errorSummary = (fetchError.message || String(fetchError)).slice(0, 120);
        if (isIntegrityBlock) {
          integrityFilteredIssues.push(issue.number);
          core.warning(`⚠️ Skipping issue #${issue.number}: blocked by integrity policy (HTTP ${status || 'unknown'}): ${errorSummary}`);
        } else {
          core.warning(`⚠️ Skipping issue #${issue.number}: could not fetch details (HTTP ${status || 'unknown'}): ${errorSummary}`);
        }
        return null;
      }

      // Check if this issue has sub-issues and linked PRs using GraphQL
      let subIssuesCount = 0;
      let linkedPRs = [];
      try {
        const issueDetailsQuery = `
          query($owner: String!, $repo: String!, $number: Int!) {
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
          }
        `;
        const issueDetailsResult = await github.graphql(issueDetailsQuery, {
          owner,
          repo,
          number: issue.number
        });

        subIssuesCount = issueDetailsResult?.repository?.issue?.subIssues?.totalCount || 0;

        // Extract linked PRs from timeline
        const timelineItems = issueDetailsResult?.repository?.issue?.timelineItems?.nodes || [];
        linkedPRs = timelineItems
          .filter(item => item?.source?.__typename === 'PullRequest')
          .map(item => ({
            number: item.source.number,
            state: item.source.state,
            isDraft: item.source.isDraft,
            author: item.source.author?.login
          }));

        core.info(`Issue #${issue.number} has ${linkedPRs.length} linked PR(s)`);
      } catch (error) {
        // If GraphQL query fails, continue with defaults
        core.warning(`Could not check details for #${issue.number}: ${error.message}`);
      }

      return {
        ...fullIssue.data,
        subIssuesCount,
        linkedPRs
      };
  };

  // Only inspect the most recent candidates (search is sorted by created desc)
  // to bound the total number of detail requests per run.
  const issuesToInspect = candidateIssues.slice(0, MAX_ISSUES_TO_INSPECT);
  if (candidateIssues.length > issuesToInspect.length) {
    core.info(`Inspecting the ${issuesToInspect.length} most recent of ${candidateIssues.length} matching issues to stay within rate limits`);
  }

  // Fetch details in bounded-concurrency batches rather than all at once.
  const detailedIssues = [];
  for (let batchStart = 0; batchStart < issuesToInspect.length; batchStart += DETAIL_FETCH_CONCURRENCY) {
    const batch = issuesToInspect.slice(batchStart, batchStart + DETAIL_FETCH_CONCURRENCY);
    const batchResults = await Promise.all(batch.map(fetchIssueDetails));
    detailedIssues.push(...batchResults);
  }
  const issuesWithDetails = detailedIssues.filter(Boolean); // Remove null entries (integrity-filtered or otherwise skipped)

  // Emit diagnostic summary for integrity-filtered issues
  if (integrityFilteredIssues.length > 0) {
    core.warning(`🛡️ Integrity filter diagnostic: ${integrityFilteredIssues.length} issue(s) were skipped due to integrity policy: #${integrityFilteredIssues.join(', #')}. These issues will be excluded from this run.`);
  }

  // Filter and score issues
  const scoredIssues = issuesWithDetails
    .filter(issue => {
      // Human assignees are ownership routing from triage, not evidence that
      // implementation has started. Only a Copilot assignee indicates that an
      // assignment was already dispatched but may not have produced a PR yet.
      const copilotAssignees = issue.assignees?.filter(assignee =>
        assignee.login === 'copilot-swe-agent' || assignee.login?.includes('copilot')
      ) || [];
      if (copilotAssignees.length > 0) {
        core.info(`Skipping #${issue.number}: already assigned to Copilot`);
        return false;
      }

      // Exclude issues with excluded labels (double check)
      const issueLabels = issue.labels.map(l => l.name.toLowerCase());
      if (issueLabels.some(label => excludeLabels.map(l => l.toLowerCase()).includes(label))) {
        core.info(`Skipping #${issue.number}: has excluded label`);
        return false;
      }

      // Exclude issues that have sub-issues (parent/organizing issues)
      if (issue.subIssuesCount > 0) {
        core.info(`Skipping #${issue.number}: has ${issue.subIssuesCount} sub-issue(s) - parent issues are used for organizing, not tasks`);
        return false;
      }

      // Any linked PR means the issue is already being or has been worked on,
      // regardless of the PR author or state.
      if (issue.linkedPRs?.length > 0) {
        core.info(`Skipping #${issue.number}: has ${issue.linkedPRs.length} linked PR(s)`);
        return false;
      }

      return true;
    })
    .map(issue => {
      const issueLabels = issue.labels.map(l => l.name.toLowerCase());
      let score = 0;

      // Score based on repository labels (higher score = higher priority)
      if (issueLabels.includes('documentation')) {
        score += 60;
      }
      if (issueLabels.includes('bug')) {
        score += 55;
      }
      if (issueLabels.includes('area-infrastructure') ||
          issueLabels.includes('test debt') ||
          issueLabels.includes('known build error')) {
        score += 50;
      }
      if (issueLabels.includes('cost:s')) {
        score += 50;
      }
      if (issueLabels.includes('good first issue') || issueLabels.includes('help wanted')) {
        score += 40;
      }
      if (issueLabels.includes('enhancement') || issueLabels.includes('fit-n-finish')) {
        score += 40;
      }
      if (issueLabels.includes('performance')) {
        score += 30;
      }

      // Bonus for issues with clear labels (any priority label)
      if (issueLabels.some(label => priorityLabels.map(l => l.toLowerCase()).includes(label))) {
        score += 10;
      }

      // Age bonus: older issues get slight priority (days old / 10)
      const ageInDays = Math.floor((Date.now() - new Date(issue.created_at)) / (1000 * 60 * 60 * 24));
      score += Math.min(ageInDays / 10, 20); // Cap age bonus at 20 points

      return {
        number: issue.number,
        title: issue.title,
        labels: issue.labels.map(l => l.name),
        body: issue.body,
        created_at: issue.created_at,
        score
      };
    })
    .sort((a, b) => b.score - a.score); // Sort by score descending
  // Format output
  const issueList = scoredIssues.map(i => {
    const labelStr = i.labels.length > 0 ? ` [${i.labels.join(', ')}]` : '';
    return `#${i.number}: ${i.title}${labelStr} (score: ${i.score.toFixed(1)})`;
  }).join('\n');

  // Pre-fetch compact body context for top candidates so the agent can
  // triage without extra reads in most runs.
  const issueContext = scoredIssues.slice(0, MAX_ISSUES_WITH_BODY_CONTEXT).map(i => {
    const body = (i.body || '').replace(/\s+/g, ' ').trim();
    const bodySnippet = body.length > BODY_SNIPPET_MAX_LENGTH ? `${body.slice(0, BODY_SNIPPET_MAX_LENGTH)}…` : body;
    const labelStr = i.labels.length > 0 ? i.labels.join(', ') : 'none';
    return `#${i.number} | score=${i.score.toFixed(1)} | labels=${labelStr}\nTitle: ${i.title}\nBody: ${bodySnippet || '(no body)'}`;
  }).join('\n\n---\n\n');

  const issueNumbers = scoredIssues.map(i => i.number).join(',');

  core.info(`Total candidate issues: ${scoredIssues.length}`);
  if (scoredIssues.length > 0) {
    core.info(`Top candidates:\n${issueList.split('\n').slice(0, 10).join('\n')}`);
  }

  core.setOutput('issue_count', scoredIssues.length);
  core.setOutput('issue_numbers', issueNumbers);
  core.setOutput('issue_list', issueList);
  core.setOutput('issue_context', issueContext);

  if (scoredIssues.length === 0) {
    core.info('🍽️ No suitable candidate issues - the plate is empty!');
    core.setOutput('has_issues', 'false');
  } else {
    core.setOutput('has_issues', 'true');
  }
} catch (error) {
  core.error(`Error searching for issues: ${error.message}`);
  core.setOutput('issue_count', 0);
  core.setOutput('issue_numbers', '');
  core.setOutput('issue_list', '');
  core.setOutput('issue_context', '');
  core.setOutput('has_issues', 'false');
}