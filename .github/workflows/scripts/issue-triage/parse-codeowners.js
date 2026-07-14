const fs = require("fs");

if (Object.keys(process.env).some(name => /token|secret|authorization|(^|_)pat(_|$)/i.test(name))) {
  throw new Error("Parser environment contains credential-like variables");
}

const [sourcePath, routingPath, teamsPath] = process.argv.slice(2);
if (!sourcePath || !routingPath || !teamsPath) {
  throw new Error("Expected CODEOWNERS, routing output, and teams output paths");
}

const source = fs.readFileSync(sourcePath, "utf8");
if (source.length > 262144) {
  throw new Error("CODEOWNERS exceeds the supported size");
}

const areaPattern = /^Area-[A-Za-z0-9][A-Za-z0-9 .+_-]{0,79}$/;
const ownerPattern = /^@[A-Za-z0-9-]+(?:\/[A-Za-z0-9_.-]+)?$/;
const pathPattern = /^[A-Za-z0-9_./*?![\]-]+$/;
const areas = new Map();
let currentAreas = [];

for (const line of source.split(/\r?\n/)) {
  const comment = line.match(/^\s*#\s*(.*)$/);
  if (comment) {
    const headings = comment[1].match(/Area-(?:(?!\s+Area-).)+/g) || [];
    currentAreas = headings.map(area => area.trim()).filter(area => areaPattern.test(area));
    for (const area of currentAreas) {
      if (!areas.has(area)) areas.set(area, []);
    }
    continue;
  }

  if (currentAreas.length === 0 || !line.trim()) continue;
  const fields = line.trim().split(/\s+/);
  const pattern = fields[0];
  if (pattern.length > 200 || !pathPattern.test(pattern)) continue;

  const owners = fields.slice(1).filter(owner => ownerPattern.test(owner));
  if (owners.length === 0) continue;
  const rule = {
    pattern,
    individual_owners: owners.filter(owner => !owner.includes("/")),
    team_owners: owners.filter(owner => owner.includes("/"))
  };
  for (const area of currentAreas) areas.get(area).push(rule);
}

const routing = {
  areas: Object.fromEntries([...areas].sort(([left], [right]) => left.localeCompare(right))),
  fallback_team: "@dotnet/dotnet-cli"
};
const teams = [...new Set([...areas.values()].flatMap(rules =>
  rules.flatMap(rule => rule.team_owners)))].sort();
const individuals = [...new Set([...areas.values()].flatMap(rules =>
  rules.flatMap(rule => rule.individual_owners)))].sort();

if (areas.size > 100 || teams.length > 100 || individuals.length > 300 ||
    [...areas.values()].reduce((count, rules) => count + rules.length, 0) > 300) {
  throw new Error("CODEOWNERS exceeds supported routing limits");
}

fs.writeFileSync(routingPath, JSON.stringify({ ...routing, teams, individuals }));
fs.writeFileSync(teamsPath, teams.map((team, index) => {
  const [organization, slug] = team.slice(1).split("/");
  return `${index}\t${organization}\t${slug}`;
}).join("\n"));
