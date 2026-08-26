# DNX package cache behavior

The `dnx` and `dotnet tool exec` commands run a .NET tool without a global installation.
Both commands use the same package resolution behavior.

The commands first check the NuGet global packages folder for a compatible tool.
An exact version request uses a matching cached tool immediately.

A floating version request can use the highest compatible cached tool.
The command checks configured feeds for a newer version for 100 milliseconds.
The command uses the cached tool when the check times out or a feed fails.
The command uses a newer feed version when the check completes in time.

The command uses normal feed resolution when no compatible cached tool exists.
Normal feed resolution does not use the 100-millisecond limit.

## Cache bypass

Use `--no-cache` to bypass cached tool selection.
You can also set the `NO_CACHE` environment variable to a true value.
True values include `1`, `true`, `yes`, and `on`.

Cache bypass requires normal feed resolution.
The command fails when the required feeds are unavailable.

## Feed check timeout

Set `DNX_FEED_TIMEOUT_MILLISECONDS` to change the feed check timeout.
Specify a positive integer value in milliseconds.

The command uses 100 milliseconds when the value is absent or invalid.
The command also uses 100 milliseconds when the value is zero or negative.
