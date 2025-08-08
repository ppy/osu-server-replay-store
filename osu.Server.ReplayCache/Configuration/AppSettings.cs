// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Server.ReplayCache.Configuration
{
    public static class AppSettings
    {
        public static StorageType? StorageType
        {
            get
            {
                string? value = Environment.GetEnvironmentVariable("REPLAY_STORAGE_TYPE");

                if (!Enum.TryParse(value, true, out StorageType storageType) || !Enum.IsDefined(storageType))
                    return null;

                return storageType;
            }
        }

        public static string LocalReplayStoragePath =>
            Environment.GetEnvironmentVariable("LOCAL_REPLAY_STORAGE_PATH")
            ?? throw new InvalidOperationException("LOCAL_REPLAY_STORAGE_PATH environment variable not set. "
                                                   + "Please set the value of this variable to the path of a directory where the replays should reside.");

        public static string LocalLegacyReplayStoragePath =>
            Environment.GetEnvironmentVariable("LOCAL_LEGACY_REPLAY_STORAGE_PATH")
            ?? throw new InvalidOperationException("LOCAL_LEGACY_REPLAY_STORAGE_PATH environment variable not set. "
                                                   + "Please set the value of this variable to the path of a directory where the legacy replays should reside.");

        public static string S3AccessKey =>
            Environment.GetEnvironmentVariable("S3_ACCESS_KEY")
            ?? throw new InvalidOperationException("S3_ACCESS_KEY environment variable not set. "
                                                   + "Please set the value of this variable to a valid Amazon S3 access key ID.");

        public static string S3SecretKey =>
            Environment.GetEnvironmentVariable("S3_SECRET_KEY")
            ?? throw new InvalidOperationException("S3_SECRET_KEY environment variable not set. "
                                                   + "Please set the value of this variable to the correct secret key for the S3_ACCESS_KEY supplied.");

        public static string S3ReplaysBucketName =>
            Environment.GetEnvironmentVariable("S3_REPLAYS_BUCKET_NAME")
            ?? throw new InvalidOperationException("S3_REPLAYS_BUCKET_NAME environment variable not set. "
                                                   + "Please set the value of this variable to the name of the bucket to be used for storing replays on S3.");

        public static string S3LegacyReplaysBucketName =>
            Environment.GetEnvironmentVariable("S3_LEGACY_REPLAYS_BUCKET_NAME")
            ?? throw new InvalidOperationException("S3_LEGACY_REPLAYS_BUCKET_NAME environment variable not set. "
                                                   + "Please set the value of this variable to the name of the bucket to be used for storing legacy replays on S3.");

        public static string S3ReplaysBucketRegion =>
            Environment.GetEnvironmentVariable("S3_REPLAYS_BUCKET_REGION")
            ?? throw new InvalidOperationException("S3_REPLAYS_BUCKET_REGION environment variable not set. "
                                                   + $"Please set the value of this variable to the region in which the \"{S3ReplaysBucketName}\" bucket exists.");

        public static string RedisHost =>
            Environment.GetEnvironmentVariable("REDIS_HOST")
            ?? throw new InvalidOperationException("REDIS_HOST environment variable not set. "
                                                   + "Please set the value of this variable to a valid Redis host.");

        public static string? SentryDsn => Environment.GetEnvironmentVariable("SENTRY_DSN");

        public static string? DatadogAgentHost => Environment.GetEnvironmentVariable("DD_AGENT_HOST");
    }

    public enum StorageType
    {
        Local,
        S3,
    }
}
