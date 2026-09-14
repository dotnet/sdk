import { spawn } from "node:child_process";
import { createHash } from "node:crypto";
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
const requestedBrowserPackageVersion =
  process.env.MTP_BROWSER_PACKAGE_VERSION;
const browserExecutable = process.env.MTP_BROWSER_EXECUTABLE;
const forwardedArguments = process.argv.slice(2);
const unsupportedLifecycleOption =
  forwardedArguments.find(
    (argument) => argument === "--no-build" || argument === "--no-restore",
  )
  ?? (Object.hasOwn(process.env, "npm_config_build")
      && process.env.npm_config_build === ""
    ? "--no-build"
    : undefined)
  ?? (Object.hasOwn(process.env, "npm_config_restore")
      && process.env.npm_config_restore === ""
    ? "--no-restore"
    : undefined);

if (dotnetPath === "dotnet") {
  console.warn(
    "The repo redist/bootstrap SDK is unavailable; using the SDK selected from PATH/global.json.",
  );
}
if (!browserPackageSource) {
  throw new Error(
    "Set MTP_BROWSER_PACKAGE_SOURCE to the folder containing Microsoft.Testing.Platform.Browser.nupkg.",
  );
}
if (!browserExecutable) {
  throw new Error(
    "Set MTP_BROWSER_EXECUTABLE to a Chromium-family browser executable.",
  );
}
if (unsupportedLifecycleOption) {
  throw new Error(
    `${unsupportedLifecycleOption} is not supported by this PoC runner because each invocation uses isolated build state.`,
  );
}

const browserPackage = findBrowserPackage(
  browserPackageSource,
  requestedBrowserPackageVersion,
);
const packageHash = hashFile(browserPackage.path, "sha256", "hex");
const packageSha512 = hashFile(browserPackage.path, "sha512", "base64");
const runDirectoryName = `${packageHash.slice(0, 12)}-${process.pid}`;
const packagesRoot = path.join(
  repoRoot,
  "artifacts",
  "tmp",
  "mtp-pkgs-no-server",
);
const restorePackagesPath = path.join(
  packagesRoot,
  packageHash.slice(0, 16),
);
const runRoot = path.join(
  repoRoot,
  "artifacts",
  "tmp",
  "mtp-run",
  runDirectoryName,
);
const projectExtensionsPath = ensureTrailingSeparator(
  path.join(runRoot, "project-extensions"),
);
const intermediateOutputPath = ensureTrailingSeparator(
  path.join(runRoot, "obj"),
);
const outputPath = ensureTrailingSeparator(path.join(runRoot, "bin"));
const keepRunOutputs = process.env.MTP_BROWSER_KEEP_RUN_OUTPUTS === "1";
const cacheLeasePath = path.join(
  restorePackagesPath,
  `.lease-${process.pid}-${Date.now()}`,
);

const commonProperties = [
  projectPath,
  "-p:BrowserWasmUseMtpPackage=true",
  `-p:BrowserWasmMtpPackageSource=${browserPackageSource}`,
  `-p:BrowserWasmMtpPackageVersion=${browserPackage.version}`,
  `-p:TestingPlatformBrowserExecutable=${browserExecutable}`,
  `-p:RestorePackagesPath=${restorePackagesPath}`,
  `-p:MSBuildProjectExtensionsPath=${projectExtensionsPath}`,
  `-p:BaseIntermediateOutputPath=${intermediateOutputPath}`,
  `-p:BaseOutputPath=${outputPath}`,
];

let activeProcess;
let stopping = false;
let signalExitCode;

await withCacheLock(() => {
  fs.mkdirSync(restorePackagesPath, { recursive: true });
  fs.writeFileSync(cacheLeasePath, `${process.pid}\n`, { flag: "wx" });
});

for (const signal of ["SIGINT", "SIGTERM"]) {
  process.once(signal, () => {
    if (signalExitCode !== undefined) {
      return;
    }

    signalExitCode = signal === "SIGINT" ? 130 : 143;
    void cleanup(signal).finally(() => process.exit(signalExitCode));
  });
}

try {
  const restoreExitCode = await runDotnet(["restore", ...commonProperties]);
  if (restoreExitCode !== 0) {
    throw new Error(`dotnet restore failed with exit code ${restoreExitCode}.`);
  }
  verifyRestoredPackage(
    restorePackagesPath,
    browserPackage.version,
    browserPackage.path,
    packageSha512,
  );

  process.exitCode = await runDotnet([
    "test",
    ...commonProperties,
    "--no-restore",
    ...forwardedArguments,
  ]);
} finally {
  await cleanup();
}

function resolveDotnetPath(root) {
  const executable = process.platform === "win32" ? "dotnet.exe" : "dotnet";
  const candidates = [
    path.join(root, "artifacts", "bin", "redist", "Debug", "dotnet", executable),
    path.join(root, ".dotnet", executable),
  ];
  return candidates.find(fs.existsSync) ?? "dotnet";
}

function findBrowserPackage(source, requestedVersion) {
  const prefix = "Microsoft.Testing.Platform.Browser.";
  const suffix = ".nupkg";
  const packages = fs
    .readdirSync(source)
    .filter((name) => name.startsWith(prefix) && name.endsWith(suffix))
    .map((name) => ({
      name,
      path: path.join(source, name),
      version: name.slice(prefix.length, -suffix.length),
    }));

  if (requestedVersion) {
    const selected = packages.find(
      (candidate) => candidate.version === requestedVersion,
    );
    if (!selected) {
      throw new Error(
        `Microsoft.Testing.Platform.Browser ${requestedVersion} was not found in '${source}'.`,
      );
    }
    return selected;
  }

  if (packages.length === 0) {
    throw new Error(
      `No Microsoft.Testing.Platform.Browser package was found in '${source}'.`,
    );
  }
  if (packages.length !== 1) {
    throw new Error(
      `Multiple Microsoft.Testing.Platform.Browser packages were found in '${source}'. Set MTP_BROWSER_PACKAGE_VERSION explicitly.`,
    );
  }
  return packages[0];
}

async function runDotnet(args) {
  const child = spawn(dotnetPath, args, {
    cwd: repoRoot,
    env: {
      ...process.env,
      DOTNET_CLI_TELEMETRY_OPTOUT: "1",
      DOTNET_CLI_USE_MSBUILD_SERVER: "0",
    },
    stdio: "inherit",
    windowsHide: true,
    detached: process.platform !== "win32",
  });
  activeProcess = child;

  try {
    const exit = await new Promise((resolve, reject) => {
      child.once("error", reject);
      child.once("exit", (code, signal) => resolve({ code, signal }));
    });

    if (exit.signal) {
      throw new Error(`dotnet ${args[0]} terminated with signal ${exit.signal}.`);
    }
    return exit.code ?? 1;
  } finally {
    if (activeProcess === child) {
      activeProcess = undefined;
    }
  }
}

function verifyRestoredPackage(packagesPath, version, packagePath, expectedHash) {
  const restoredHashPath = path.join(
    packagesPath,
    "microsoft.testing.platform.browser",
    version.toLowerCase(),
    `microsoft.testing.platform.browser.${version.toLowerCase()}.nupkg.sha512`,
  );
  const restoredHash = fs.readFileSync(restoredHashPath, "utf8").trim();
  if (restoredHash !== expectedHash) {
    throw new Error(
      `The restored Microsoft.Testing.Platform.Browser ${version} package does not match '${packagePath}'.`,
    );
  }
}

function hashFile(filePath, algorithm, encoding) {
  return createHash(algorithm).update(fs.readFileSync(filePath)).digest(encoding);
}

function ensureTrailingSeparator(value) {
  return value.endsWith(path.sep) ? value : `${value}${path.sep}`;
}

async function cleanup(signal = "SIGTERM") {
  if (stopping) {
    return;
  }

  stopping = true;
  try {
    await stopProcess(activeProcess, signal);
  } catch (error) {
    console.error(`Failed to stop the active dotnet command: ${error}`);
  }

  if (!keepRunOutputs) {
    removeDirectory(runRoot, "browser test run directory");
  }
  try {
    await withCacheLock(() => {
      try {
        fs.rmSync(cacheLeasePath, { force: true });
      } catch (error) {
        console.error(`Failed to remove package cache lease '${cacheLeasePath}': ${error}`);
      }
      prunePackageCaches(packagesRoot, restorePackagesPath, 2);
    });
  } catch (error) {
    console.error(`Failed to maintain the browser package cache: ${error}`);
  }
}

async function stopProcess(child, signal) {
  if (!child || child.exitCode !== null || child.signalCode !== null) {
    return;
  }

  if (process.platform === "win32") {
    await stopWindowsProcessTree(child.pid);
    return;
  }

  process.kill(-child.pid, signal);
  try {
    await withTimeout(
      new Promise((resolve) => child.once("exit", resolve)),
      5_000,
      "Timed out waiting for dotnet to exit.",
    );
  } catch {
    process.kill(-child.pid, "SIGKILL");
  }
}

async function stopWindowsProcessTree(pid) {
  const escapedScript = `
    $rootPid = ${pid}
    $all = Get-CimInstance Win32_Process
    $ids = New-Object System.Collections.Generic.List[int]
    function Add-Children([int]$parent) {
      foreach ($child in $all | Where-Object ParentProcessId -eq $parent) {
        Add-Children $child.ProcessId
        $ids.Add([int]$child.ProcessId)
      }
    }
    Add-Children $rootPid
    $ids.Add($rootPid)
    foreach ($id in $ids) {
      Stop-Process -Id $id -Force -ErrorAction SilentlyContinue
    }
  `;

  const child = spawn(
    "powershell",
    ["-NoProfile", "-NonInteractive", "-Command", escapedScript],
    {
      cwd: pocDirectory,
      stdio: "inherit",
      windowsHide: true,
    },
  );
  await new Promise((resolve, reject) => {
    child.once("error", reject);
    child.once("exit", resolve);
  });
}

function removeDirectory(directory, description) {
  try {
    fs.rmSync(directory, { recursive: true, force: true });
  } catch (error) {
    console.error(`Failed to remove the ${description} '${directory}': ${error}`);
  }
}

function prunePackageCaches(root, current, maximumCount) {
  if (!fs.existsSync(root)) {
    return;
  }

  const caches = fs
    .readdirSync(root, { withFileTypes: true })
    .filter((entry) => entry.isDirectory() && entry.name !== ".maintenance-lock")
    .flatMap((entry) => {
      const cachePath = path.join(root, entry.name);
      try {
        const hasLease = fs
          .readdirSync(cachePath)
          .some((name) => name.startsWith(".lease-"));
        return hasLease
          ? []
          : [{
              path: cachePath,
              modified: fs.statSync(cachePath).mtimeMs,
            }];
      } catch {
        return [];
      }
    })
    .sort((left, right) => {
      if (left.path === current) {
        return -1;
      }
      if (right.path === current) {
        return 1;
      }
      return right.modified - left.modified;
    });

  for (const cache of caches.slice(maximumCount)) {
    removeDirectory(cache.path, "stale browser package cache");
  }
}

async function withCacheLock(action) {
  fs.mkdirSync(packagesRoot, { recursive: true });
  const lockPath = path.join(packagesRoot, ".maintenance-lock");
  const deadline = Date.now() + 30_000;

  while (true) {
    try {
      fs.mkdirSync(lockPath);
      break;
    } catch (error) {
      if (error.code !== "EEXIST" || Date.now() >= deadline) {
        throw new Error(`Unable to acquire browser package cache lock '${lockPath}'.`, {
          cause: error,
        });
      }
      try {
        if (Date.now() - fs.statSync(lockPath).mtimeMs > 60_000) {
          fs.rmSync(lockPath, { recursive: true, force: true });
          continue;
        }
      } catch (statError) {
        if (statError.code !== "ENOENT") {
          console.error(`Unable to inspect browser package cache lock '${lockPath}': ${statError}`);
        }
      }
      await new Promise((resolve) => setTimeout(resolve, 100));
    }
  }

  try {
    return action();
  } finally {
    fs.rmSync(lockPath, { recursive: true, force: true });
  }
}

async function withTimeout(promise, milliseconds, message) {
  let timer;
  try {
    return await Promise.race([
      promise,
      new Promise((_, reject) => {
        timer = setTimeout(() => reject(new Error(message)), milliseconds);
      }),
    ]);
  } finally {
    clearTimeout(timer);
  }
}
