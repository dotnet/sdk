# Success Criteria

## Stage A Success Criteria

Stage A reduces initial implementation complexity and scopes bugs to the first set of restrictions. Waiting and transparent re-running of `non-safe` commands are deferred to Stage B; the selected design must preserve the ability to add that behavior without replacing the locking protocol.

- Abrupt termination or power loss may leave the canonical executable unavailable. Consumers must be able to re-acquire it using the [installation scripts](https://aka.ms/dotnet/dotnetup). The protocol does not promise crash-atomic or power-loss-atomic recovery.<br><br>


- Recovery retains transaction-specific backups and uses transaction state while holding both locks. It does not need the rejected candidate's version or require that candidate to execute. Self-update does not migrate SDK/runtime installation state. Reinstallation is the fallback when recovery cannot establish a runnable canonical executable; these rules are not a guarantee against arbitrary external file substitutions or filesystem damage after power loss.<br><br>

- Multiple `dotnetup` processes in general must be able to execute at the same time.<br><br>

- `dotnetup` may leverage asynchronous code or `await`.<br><br>

- `self update` must not run if any `non-safe` `dotnetup` process is currently running. e.g. if `dotnetup sdk install` is running, the manifest format may change from one version to another; installing a new version that may edit the manifest format may cause the old `dotnetup` process to fail, and we want an invariant that avoids any such bugs.<br><br>

- `non-safe` processes must never execute their command body across a `self update` boundary. In Stage A, a newly started `non-safe` process fails immediately at its gate if a self update is in progress. If its executable was replaced before it passed the gate, it fails and instructs the caller to re-run the command; it must never execute its now-stale command body.<br><br>

- Stage A documentation must warn callers that `dotnetup` commands classified as `non-safe`, including `dotnetup --info`, may fail while a self update is running. Callers are responsible for retrying after the update completes.<br><br>

- `self update` does NOT make other `self update` processes fail or exit immediately; other `self update` processes must merely wait for the other update processes to complete and then determine that an update is no longer needed, assuming no release occurs within the time frame of the race.<br><br>

- At this time, the only 'safe' `dotnetup` processes to have running during `self update` are the `telemetry drain` process, `dotnetup dotnet`, and the `self update` process itself. All other processes are `non-safe`. A process does not know its 'safety' status until `S.CL` parsers or `args` are processed.<br><br>

- `dotnetup` must not require a reboot to update itself.<br><br>

- It's ok to ignore a 'rogue' `dotnetup` process and allow them to fail or incur behavioral runtime bugs; e.g. an old `dotnetup` version that does not know about or support any mutex, semaphore, or locks and therefore bypasses the conditional guarantees. This is permissible because `dotnetup` is not yet `stable` or in a fully public `preview`.

## Stage B Success Criteria

Stage B retains the Stage A safety restrictions and replaces its fail-fast behavior for `non-safe` processes with waiting and transparent re-running by default. Concurrent `self update` calls wait in both stages; that serialization is included in Stage A because it is less complex than transparently re-running arbitrary commands.

- `self update` also does NOT make other `non safe` processes fail or exit immediately unless they are configured to do so. This is because we don't want others who call `dotnetup --info` to have to worry about whether another process is running `self update` or have to write recovery logic for this. However, others may opt in to this behavior if they want minimal latency and would rather defer the task if an update is running.<br><br>

- A `non-safe` process that starts while `self update` is running waits at its gate rather than failing outright by default, so `dotnetup list` and IDE-issued commands do not hard-fail merely because an update is in progress. If the executable was replaced before it passed the gate, it must transparently forward the invocation to the updated executable and return that process's exit code; it must never resume running its own now-stale code. If breaking changes are made to command names themselves, then this will break and that is acceptable.<br><br>

