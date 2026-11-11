const assert = require("node:assert/strict");
const {readFileSync} = require("node:fs");
const path = require("node:path");
const {test} = require("node:test");

const {
    DEFAULT_TEAM,
    countRecentAssignments,
    createGitHubClient,
    findAreaOwners,
    parseHeadingAreas,
    resolveCandidates,
    runSelectAssignee,
    selectAssignee,
} = require("./issue-triage-assignee.js");

const repositoryRoot = path.join(__dirname, "..", "..");
const codeowners = readFileSync(path.join(repositoryRoot, "CODEOWNERS"), "utf8");
const snapshot = require("./issue-triage-team-snapshot.json");
const now = new Date("2026-11-11T12:00:00Z");
const nugetTeam = snapshot["@dotnet/nuget-team"];

function createClient(pages = [[]])
{
    const requests = [];
    return {
        requests,
        async getJson(resource)
        {
            requests.push(resource);
            return {items: pages[requests.length - 1] ?? []};
        },
    };
}

function assignedIssues(...logins)
{
    return logins.map((login) => ({assignees: [{login}]}));
}

function select(area, client = createClient(), randomInt = () => 0)
{
    return selectAssignee({area, codeowners, snapshot, client, repository: "dotnet/sdk", now, randomInt});
}

test("headings yield every complete Area-* name, including names with spaces", () =>
{
    assert.deepEqual(parseHeadingAreas("# Area-ILLink Area-ReadyToRun"), ["area-illink", "area-readytorun"]);
    assert.deepEqual(parseHeadingAreas("# Area-DotNet Test"), ["area-dotnet test"]);
    assert.deepEqual(parseHeadingAreas("# AI"), []);
    assert.deepEqual(parseHeadingAreas("# Publish.targets related to ILLink and ReadyToRun is own by both runtime and SDK team"), []);
    assert.deepEqual(parseHeadingAreas("/src/Area-Fake @dotnet/dotnet-cli"), []);
});

test("CODEOWNERS sections include in-section notes but stop at the next section", () =>
{
    assert.deepEqual(findAreaOwners(codeowners, "Area-NuGet").owners, ["@dotnet/nuget-team"]);
    assert.deepEqual(findAreaOwners(codeowners, "area-readytorun").owners, ["@dotnet/illink", "@dotnet/dotnet-cli"]);
    assert.deepEqual(findAreaOwners(codeowners, "Area-Infrastructure").owners, ["@MiYanni"]);
    assert.deepEqual(findAreaOwners(codeowners, "Area-Watch").owners, ["@dotnet/hot-reload", "@dotnet/aspnet-blazor-eng"]);
    assert.deepEqual(findAreaOwners(codeowners, "Area-dotnet test (MTP)"), {matched: false, owners: []});
});

test("the last matching CODEOWNERS section wins", () =>
{
    const duplicated = "# Area-Format\n/old @old-owner\n\n# AI\n.github/ @ai-owner\n\n# Area-Format\n/new @dotnet/roslyn-ide\n";
    assert.deepEqual(findAreaOwners(duplicated, "Area-Format").owners, ["@dotnet/roslyn-ide"]);
});

test("team owners expand through the snapshot without duplicate individuals", () =>
{
    const {teams, individuals} = resolveCandidates(["@dotnet/dotnetup", "@nagilson", "@dotnet/run-file", "@bad/../path"], snapshot);
    assert.deepEqual(teams, ["@dotnet/dotnetup", "@dotnet/run-file"]);
    assert.deepEqual(individuals, ["dsplaisted", "marcpopMSFT", "nagilson"]);
});

test("every snapshot and CODEOWNERS team can be mentioned by the triage comment", () =>
{
    const workflow = readFileSync(path.join(repositoryRoot, ".github", "workflows", "issue-triage.md"), "utf8");
    const allowed = new Set([...workflow.matchAll(/^ +- (dotnet\/[\w.-]+)\r?$/gm)].map((match) => `@${match[1]}`));
    const codeownersTeams = codeowners.match(/@dotnet\/[\w.-]+/g);
    for (const team of [...Object.keys(snapshot), ...codeownersTeams])
    {
        assert.ok(allowed.has(team), `${team} is missing from safe-outputs.mentions.allowed`);
    }
});

test("the lowest recent load wins and reports the overridden random candidate", async () =>
{
    const busy = assignedIssues("nkolev92", "nkolev92", ...nugetTeam.filter((login) => login !== "jebriede"));
    const result = await select("Area-NuGet", createClient([busy]), (max) => max - 1);
    assert.equal(result.assignee, "jebriede");
    assert.deepEqual(result.teams, ["@dotnet/nuget-team"]);
    assert.equal(result.needsTeamTriage, false);
    assert.deepEqual(result.lowerLoadOverride, {initial: "nkolev92", initialCount: 3, selectedCount: 0});
    assert.equal(result.load.nkolev92, 3);
    assert.equal("jebriede" in result.load, false);
});

test("equal load keeps the random order and reports no override", async () =>
{
    const result = await select("Area-NuGet", createClient(), (max) => max - 1);
    assert.equal(result.assignee, "nkolev92");
    assert.equal(result.lowerLoadOverride, null);
});

test("equal load selects every candidate with near-uniform frequency", async () =>
{
    const trials = 2000;
    const selections = new Map();
    for (let trial = 0; trial < trials; trial++)
    {
        const {assignee} = await selectAssignee({area: "Area-NuGet", codeowners, snapshot, client: createClient(), repository: "dotnet/sdk", now});
        selections.set(assignee, (selections.get(assignee) ?? 0) + 1);
    }

    assert.deepEqual([...selections.keys()].sort(), [...nugetTeam].sort());
    const expected = trials / nugetTeam.length;
    for (const [login, count] of selections)
    {
        assert.ok(Math.abs(count - expected) < expected * 0.4, `${login} selected ${count} times; expected about ${expected}`);
    }
});

test("search failure still selects a random candidate and exposes the failure", async () =>
{
    const client = {async getJson() { throw new Error("GitHub API /search/issues returned HTTP 403."); }};
    const result = await select("Area-NuGet", client, () => 0);
    assert.ok(nugetTeam.includes(result.assignee));
    assert.equal(result.load, null);
    assert.equal(result.lowerLoadOverride, null);
    assert.match(result.loadError, /HTTP 403/);
});

test("teams without snapshot members and unknown areas need team triage", async () =>
{
    const runFile = await select("Area-run-file");
    assert.deepEqual([runFile.assignee, runFile.teams, runFile.needsTeamTriage], [null, ["@dotnet/run-file"], true]);
    const unknown = await select("Area-Unknown");
    assert.deepEqual([unknown.assignee, unknown.teams, unknown.needsTeamTriage], [null, [DEFAULT_TEAM], true]);
});

for (const area of ["", "NuGet", "Area-NuGet\n# Area-Infrastructure", `Area-${"x".repeat(100)}`, null])
{
    test(`invalid area ${JSON.stringify(area)?.slice(0, 30)} is rejected before contacting GitHub`, async () =>
    {
        const client = {async getJson() { assert.fail("Must not contact GitHub"); }};
        await assert.rejects(select(area, client), /area must be one Area-\* label/);
    });
}

test("load search counts only recent open issues and stops at the last page", async () =>
{
    const fullPage = Array.from({length: 100}, () => ({assignees: [{login: "ZIVKAN"}]}));
    const client = createClient([fullPage, assignedIssues("zivkan", "someone-else")]);
    const counts = await countRecentAssignments({client, repository: "dotnet/sdk", candidates: ["zivkan", "jeffkl"], now});
    assert.deepEqual([...counts], [["zivkan", 101], ["jeffkl", 0]]);
    assert.equal(client.requests.length, 2);
    const query = decodeURIComponent(client.requests[0].match(/q=([^&]+)/)[1]);
    assert.equal(query, "repo:dotnet/sdk is:issue is:open created:>=2026-11-04");
});

test("load search is bounded when every page is full", async () =>
{
    const fullPage = Array.from({length: 100}, () => ({assignees: []}));
    const client = createClient(Array.from({length: 10}, () => fullPage));
    await countRecentAssignments({client, repository: "dotnet/sdk", candidates: ["zivkan"], now});
    assert.equal(client.requests.length, 5);
});

test("GitHub client authenticates requests and keeps the token out of errors", async () =>
{
    const calls = [];
    const fetchImpl = async (url, options) =>
    {
        calls.push({url, authorization: options.headers.Authorization});
        return {ok: false, status: 403};
    };
    const client = createGitHubClient({token: "fixture-token", fetchImpl});
    await assert.rejects(client.getJson("/search/issues?q=secret-query"), (error) =>
    {
        assert.equal(error.message, "GitHub API /search/issues returned HTTP 403.");
        assert.doesNotMatch(error.message, /fixture-token|secret-query/);
        return true;
    });
    assert.deepEqual(calls, [{url: "https://api.github.com/search/issues?q=secret-query", authorization: "Bearer fixture-token"}]);
    assert.throws(() => createGitHubClient({token: ""}), /GH_TOKEN is required/);
});

test("the entry point reads repository inputs from the checkout", async () =>
{
    const fetchImpl = async () => ({ok: true, async json() { return {items: []}; }});
    const result = await runSelectAssignee({area: "Area-dotnetup", env: {GITHUB_REPOSITORY: "dotnet/sdk", GH_TOKEN: "token"}, fetchImpl});
    assert.ok(snapshot["@dotnet/dotnetup"].includes(result.assignee));
    assert.deepEqual(result.teams, ["@dotnet/dotnetup"]);
    await assert.rejects(runSelectAssignee({area: "Area-dotnetup", env: {GITHUB_REPOSITORY: "../sdk", GH_TOKEN: "token"}, fetchImpl}), /owner\/name/);
});
