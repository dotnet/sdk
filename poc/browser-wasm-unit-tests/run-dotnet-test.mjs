import { spawn } from "node:child_process";
import fs from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import process from "node:process";

const pocDirectory = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(pocDirectory, "..", "..");
const projectPath = path.join(
  repoRoot,
  "test",
  "TestAssets",
  "TestProjects",
  "BlazorWasmTestApp",
  "BlazorWasmTestApp.csproj",
);
const dotnetPath = resolveDotnetPath(repoRoot);
const browserPackageSource = process.env.MTP_BROWSER_PACKAGE_SOURCE;
const browserExecutable = process.env.MTP_BROWSER_EXECUTABLE;

if (dotnetPath === "dotnet") {
  console.warn(
    "The repo redist/bootstrap SDK is unavailable; using the SDK selected from PATH/global.json.",
  );
}
if (!browserPackageSource) {
  throw new Error("Set MTP_BROWSER_PACKAGE_SOURCE to the folder containing Microsoft.Testing.Platform.Browser.nupkg.");
}
if (!browserExecutable) {
  throw new Error("Set MTP_BROWSER_EXECUTABLE to a Chromium-family browser executable.");
}

const browserPackageVersion = findBrowserPackageVersion(browserPackageSource);

const args = [
  "test",
  projectPath,
  "-p:BrowserWasmUseMtpPackage=true",
  `-p:BrowserWasmMtpPackageSource=${browserPackageSource}`,
  `-p:BrowserWasmMtpPackageVersion=${browserPackageVersion}`,
  `-p:TestingPlatformBrowserExecutable=${browserExecutable}`,
  ...process.argv.slice(2),
];
const child = spawn(dotnetPath, args, {
  cwd: repoRoot,
  env: {
    ...process.env,
    DOTNET_CLI_TELEMETRY_OPTOUT: "1",
  },
  stdio: "inherit",
  windowsHide: true,
});

const exit = await new Promise((resolve, reject) => {
  child.once("error", reject);
  child.once("exit", (code, signal) => resolve({ code, signal }));
});

if (exit.signal) {
  throw new Error(`dotnet test terminated with signal ${exit.signal}.`);
}

process.exitCode = exit.code ?? 1;

function resolveDotnetPath(root) {
  const executable = process.platform === "win32" ? "dotnet.exe" : "dotnet";
  const candidates = [
    path.join(root, "artifacts", "bin", "redist", "Debug", "dotnet", executable),
    path.join(root, ".dotnet", executable),
  ];
  return candidates.find(fs.existsSync) ?? "dotnet";
}

function findBrowserPackageVersion(source) {
  const prefix = "Microsoft.Testing.Platform.Browser.";
  const suffix = ".nupkg";
  const packages = fs
    .readdirSync(source)
    .filter((name) => name.startsWith(prefix) && name.endsWith(suffix))
    .sort();
  const packageName = packages.at(-1);
  if (!packageName) {
    throw new Error(`No Microsoft.Testing.Platform.Browser package was found in '${source}'.`);
  }
  return packageName.slice(prefix.length, -suffix.length);
}
