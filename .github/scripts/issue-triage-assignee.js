const crypto = require("node:crypto");
const {readFileSync} = require("node:fs");
const path = require("node:path");

// Routing defaults
const DEFAULT_TEAM = "@dotnet/dotnet-cli";
const MAX_AREA_LENGTH = 100;

// Load balancing
const LOAD_WINDOW_DAYS = 7;
const MILLISECONDS_PER_DAY = 24 * 60 * 60 * 1000;
const SEARCH_PAGE_SIZE = 100;
const MAX_SEARCH_PAGES = 5;

// GitHub API
const DEFAULT_API_URL = "https://api.github.com";
const API_VERSION = "2022-11-28";
const REQUEST_TIMEOUT_MS = 15000;

// Identifier formats
const AREA_LABEL_PATTERN = /^Area-[^\r\n]+$/i;
const AREA_HEADING_PATTERN = /^#\s*Area-/i;
const AREA_NAME_PATTERN = /Area-.*?(?=\s+Area-|$)/gi;
const LOGIN_PATTERN = /^[A-Za-z0-9][A-Za-z0-9-]{0,38}$/;
const TEAM_PATTERN = /^[A-Za-z0-9-]+\/[A-Za-z0-9._-]+$/;
const REPOSITORY_PATTERN = /^[A-Za-z0-9-]+\/[A-Za-z0-9._-]+$/;

const REPOSITORY_ROOT = path.resolve(__dirname, "..", "..");
const TEAM_SNAPSHOT_PATH = path.join(__dirname, "issue-triage-team-snapshot.json");

function normalizeArea(area)
{
    const trimmed = typeof area === "string" ? area.trim() : "";
    if (trimmed.length > MAX_AREA_LENGTH || !AREA_LABEL_PATTERN.test(trimmed))
    {
        throw new Error("area must be one Area-* label.");
    }

    return trimmed;
}

function uniqueIgnoringCase(values)
{
    const seen = new Set();
    return values.filter((value) =>
    {
        const key = value.toLowerCase();
        if (seen.has(key))
        {
            return false;
        }

        seen.add(key);
        return true;
    });
}

function parseHeadingAreas(line)
{
    const trimmed = line.trim();
    if (!AREA_HEADING_PATTERN.test(trimmed))
    {
        return [];
    }

    const heading = trimmed.replace(/^#+\s*/, "");
    return (heading.match(AREA_NAME_PATTERN) ?? []).map((name) => name.trim().toLowerCase());
}

function findAreaHeadingIndex(lines, area)
{
    const target = area.toLowerCase();
    for (let index = lines.length - 1; index >= 0; index--)
    {
        if (parseHeadingAreas(lines[index]).includes(target))
        {
            return index;
        }
    }

    return -1;
}

// A section ends at the next Area-* heading or at a comment that follows a blank line.
// Comments directly below owner lines are in-section notes, such as the ILLink Publish.targets note.
function collectSectionOwners(lines, headingIndex)
{
    const owners = [];
    let previousLineBlank = false;
    for (let index = headingIndex + 1; index < lines.length; index++)
    {
        const line = lines[index].trim();
        if (line.length === 0)
        {
            previousLineBlank = true;
            continue;
        }

        if (line.startsWith("#"))
        {
            if (previousLineBlank || parseHeadingAreas(line).length > 0)
            {
                break;
            }

            continue;
        }

        previousLineBlank = false;
        owners.push(...line.split(/\s+/).slice(1).filter((token) => token.startsWith("@")));
    }

    return owners;
}

function findAreaOwners(codeowners, area)
{
    const lines = codeowners.split(/\r?\n/);
    const headingIndex = findAreaHeadingIndex(lines, area);
    if (headingIndex < 0)
    {
        return {matched: false, owners: []};
    }

    return {matched: true, owners: uniqueIgnoringCase(collectSectionOwners(lines, headingIndex))};
}

function resolveCandidates(owners, snapshot)
{
    const membersByTeam = new Map(Object.entries(snapshot).map(([team, members]) => [team.toLowerCase(), members]));
    const teams = [];
    const individuals = [];
    for (const owner of owners)
    {
        const handle = owner.slice(1);
        if (TEAM_PATTERN.test(handle))
        {
            teams.push(owner);
            individuals.push(...(membersByTeam.get(owner.toLowerCase()) ?? []));
        }
        else if (LOGIN_PATTERN.test(handle))
        {
            individuals.push(handle);
        }
    }

    return {teams, individuals: uniqueIgnoringCase(individuals.filter((login) => LOGIN_PATTERN.test(login)))};
}

function createGitHubClient({token, apiUrl = DEFAULT_API_URL, fetchImpl = fetch})
{
    if (!token)
    {
        throw new Error("GH_TOKEN is required.");
    }

    return {
        async getJson(resource)
        {
            const response = await fetchImpl(`${apiUrl}${resource}`, {
                headers: {
                    "Accept": "application/vnd.github+json",
                    "Authorization": `Bearer ${token}`,
                    "User-Agent": "dotnet-sdk-issue-triage",
                    "X-GitHub-Api-Version": API_VERSION,
                },
                signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
            });
            if (!response.ok)
            {
                throw new Error(`GitHub API ${resource.split("?")[0]} returned HTTP ${response.status}.`);
            }

            return response.json();
        },
    };
}

function loadWindowStart(now)
{
    return new Date(now.getTime() - LOAD_WINDOW_DAYS * MILLISECONDS_PER_DAY).toISOString().slice(0, 10);
}

function tallyAssignees(items, counts)
{
    for (const item of items)
    {
        for (const assignee of item.assignees ?? [])
        {
            const key = assignee.login?.toLowerCase();
            if (counts.has(key))
            {
                counts.set(key, counts.get(key) + 1);
            }
        }
    }
}

// Counts open issues created in the load window that are assigned to each candidate.
async function countRecentAssignments({client, repository, candidates, now})
{
    const counts = new Map(candidates.map((login) => [login.toLowerCase(), 0]));
    const query = encodeURIComponent(`repo:${repository} is:issue is:open created:>=${loadWindowStart(now)}`);
    for (let page = 1; page <= MAX_SEARCH_PAGES; page++)
    {
        const result = await client.getJson(`/search/issues?q=${query}&per_page=${SEARCH_PAGE_SIZE}&page=${page}`);
        const items = result.items ?? [];
        tallyAssignees(items, counts);
        if (items.length < SEARCH_PAGE_SIZE)
        {
            break;
        }
    }

    return counts;
}

function shuffle(values, randomInt)
{
    const shuffled = [...values];
    for (let index = shuffled.length - 1; index > 0; index--)
    {
        const swapIndex = randomInt(index + 1);
        [shuffled[index], shuffled[swapIndex]] = [shuffled[swapIndex], shuffled[index]];
    }

    return shuffled;
}

// Array.prototype.sort is stable, so candidates with equal load keep their random order.
function sortByLoad(candidates, counts)
{
    return [...candidates].sort((left, right) => counts.get(left.toLowerCase()) - counts.get(right.toLowerCase()));
}

function createResult({area, teams, candidates = [], assignee = null, initial = null, counts = null, loadError = null})
{
    const countFor = (login) => counts.get(login.toLowerCase());
    return {
        area,
        assignee,
        teams,
        needsTeamTriage: assignee === null,
        lowerLoadOverride: counts && assignee !== initial
            ? {initial, initialCount: countFor(initial), selectedCount: countFor(assignee)}
            : null,
        // Zero counts are omitted so large candidate sets stay within the inline MCP script output limit.
        load: counts ? Object.fromEntries(candidates.filter((login) => countFor(login) > 0).map((login) => [login, countFor(login)])) : null,
        loadError,
    };
}

async function selectAssignee({area, codeowners, snapshot, client, repository, now = new Date(), randomInt = crypto.randomInt})
{
    const normalizedArea = normalizeArea(area);
    const section = findAreaOwners(codeowners, normalizedArea);
    if (section.owners.length === 0)
    {
        return createResult({area: normalizedArea, teams: [DEFAULT_TEAM]});
    }

    const {teams, individuals} = resolveCandidates(section.owners, snapshot);
    if (individuals.length === 0)
    {
        return createResult({area: normalizedArea, teams: teams.length > 0 ? teams : [DEFAULT_TEAM]});
    }

    let counts = null;
    let loadError = null;
    try
    {
        counts = await countRecentAssignments({client, repository, candidates: individuals, now});
    } catch (error)
    {
        loadError = error.message;
    }

    const shuffled = shuffle(individuals, randomInt);
    const ranked = counts ? sortByLoad(shuffled, counts) : shuffled;
    return createResult({area: normalizedArea, teams, candidates: individuals, assignee: ranked[0], initial: shuffled[0], counts, loadError});
}

async function runSelectAssignee({area, env = process.env, fetchImpl = fetch, repositoryRoot = REPOSITORY_ROOT})
{
    const repository = env.GITHUB_REPOSITORY ?? "";
    if (!REPOSITORY_PATTERN.test(repository))
    {
        throw new Error("GITHUB_REPOSITORY must be owner/name.");
    }

    return selectAssignee({
        area,
        codeowners: readFileSync(path.join(repositoryRoot, "CODEOWNERS"), "utf8"),
        snapshot: JSON.parse(readFileSync(TEAM_SNAPSHOT_PATH, "utf8")),
        client: createGitHubClient({token: env.GH_TOKEN, apiUrl: env.GITHUB_API_URL || DEFAULT_API_URL, fetchImpl}),
        repository,
    });
}

module.exports = {
    DEFAULT_TEAM,
    countRecentAssignments,
    createGitHubClient,
    findAreaOwners,
    parseHeadingAreas,
    resolveCandidates,
    runSelectAssignee,
    selectAssignee,
};

if (require.main === module)
{
    // Always emit JSON so the agent can follow the documented failure path.
    runSelectAssignee({area: process.env.INPUT_AREA})
        .catch((error) => ({error: error.message}))
        .then((result) => process.stdout.write(`${JSON.stringify(result)}\n`));
}
