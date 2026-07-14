const fs = require("fs");
const path = require("path");

if (Object.keys(process.env).some(name => /token|secret|authorization|(^|_)pat(_|$)/i.test(name))) {
  throw new Error("Parser environment contains credential-like variables");
}

const [routingPath, responsesPath, expandedPath, loadRequestPath, repository] = process.argv.slice(2);
if (!routingPath || !responsesPath || !expandedPath || !loadRequestPath ||
    !/^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/.test(repository)) {
  throw new Error("Expected routing, responses, expanded routing, load request, and repository");
}

const routing = JSON.parse(fs.readFileSync(routingPath, "utf8"));
if (!Array.isArray(routing.teams) || !Array.isArray(routing.individuals)) {
  throw new Error("Invalid routing input");
}

const loginPattern = /^@[A-Za-z0-9-]{1,39}$/;
if (routing.individuals.some(login => typeof login !== "string" || !loginPattern.test(login))) {
  throw new Error("Invalid individual owner in routing input");
}
const teamMembers = {};
const candidates = new Set(routing.individuals);
for (const [index, team] of routing.teams.entries()) {
  if (typeof team !== "string" || !/^@[A-Za-z0-9-]+\/[A-Za-z0-9_.-]+$/.test(team)) {
    throw new Error("Invalid team in routing input");
  }

  const responseFile = path.join(responsesPath, `team-${index}.json`);
  if (fs.statSync(responseFile).size > 10485760) {
    throw new Error("Team response exceeds the supported size");
  }
  const pages = JSON.parse(fs.readFileSync(responseFile, "utf8"));
  if (!Array.isArray(pages) || pages.length > 100 || pages.some(page => !Array.isArray(page))) {
    throw new Error("Invalid paginated team response");
  }

  const members = new Set();
  for (const member of pages.flat()) {
    const login = typeof member?.login === "string" ? `@${member.login}` : "";
    if (!loginPattern.test(login)) throw new Error("Invalid team member login");
    members.add(login);
    candidates.add(login);
  }
  teamMembers[team] = [...members].sort();
}

const sortedCandidates = [...candidates].sort();
if (sortedCandidates.length > 500) {
  throw new Error("Expanded CODEOWNERS candidate set exceeds the supported size");
}
const cutoff = new Date(Date.now() - 14 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
const selections = sortedCandidates.map((login, index) =>
  `u${index}: search(type: ISSUE, query: ${JSON.stringify(`repo:${repository} is:issue is:open assignee:${login.slice(1)} created:>=${cutoff}`)}) { issueCount }`);

fs.writeFileSync(expandedPath, JSON.stringify({ ...routing, team_members: teamMembers, candidates: sortedCandidates, cutoff_date: cutoff }));
fs.writeFileSync(loadRequestPath, JSON.stringify({ query: `query { ${selections.join(" ") || "__typename"} }` }));
