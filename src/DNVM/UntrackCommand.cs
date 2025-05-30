using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dnvm;

public sealed class UntrackCommand
{
    public abstract record Result
    {
        private Result() { }


        public sealed record Success(Manifest Manifest) : Result;
        public sealed record ChannelUntracked : Result;
        public record ManifestReadError : Result;
    }

    public static async Task<int> Run(DnvmEnv env, Logger logger, Channel channel)
    {
        Manifest manifest;
        try
        {
            manifest = await env.ReadManifest();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.Error("Failed to read manifest file");
            return 1;
        }
        var result = RunHelper(channel, manifest, logger);
        if (result is Result.Success({} newManifest))
        {
            env.WriteManifest(newManifest);
            return 0;
        }
        return 1;
    }

    public static Result RunHelper(Channel channel, Manifest manifest, Logger logger)
    {
        if (!manifest.TrackedChannels().Any(c => c.ChannelName == channel))
        {
            logger.Log("Channel '{channel}' is not tracked");
            return new Result.ChannelUntracked();
        }

        return new Result.Success(manifest.UntrackChannel(channel));
    }
}