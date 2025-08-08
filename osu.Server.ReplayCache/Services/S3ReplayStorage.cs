// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using osu.Server.ReplayCache.Configuration;
using osu.Server.ReplayCache.Helpers;

namespace osu.Server.ReplayCache.Services
{
    public class S3ReplayStorage : IReplayStorage
    {
        private readonly ILogger<S3ReplayStorage> logger;
        private readonly AmazonS3Client s3Client;

        public S3ReplayStorage(ILogger<S3ReplayStorage> logger)
        {
            this.logger = logger;

            s3Client = new AmazonS3Client(
                new BasicAWSCredentials(AppSettings.S3AccessKey, AppSettings.S3SecretKey),
                new AmazonS3Config
                {
                    CacheHttpClient = true,
                    HttpClientCacheSize = 32,
                    RegionEndpoint = RegionEndpoint.GetBySystemName(AppSettings.S3ReplaysBucketRegion),
                    UseHttp = true,
                    ForcePathStyle = true,
                    RetryMode = RequestRetryMode.Legacy,
                    MaxErrorRetry = 5,
                    Timeout = TimeSpan.FromMinutes(1),
                });
        }

        public async Task StoreReplayAsync(long scoreId, ushort rulesetId, bool legacyScore, Stream replayData)
        {
            logger.LogInformation($"Uploading replay for score {scoreId} (ruleset: {rulesetId}, legacy: {legacyScore})");

            long length = replayData.Length;

            await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = legacyScore ? getLegacyBucket(rulesetId) : AppSettings.S3ReplaysBucketName,
                Key = getPathToReplay(scoreId),
                Headers =
                {
                    ContentLength = length,
                },
                InputStream = replayData
            });
        }

        public async Task<Stream> GetReplayStreamAsync(long scoreId, ushort rulesetId, bool legacyScore)
        {
            var memoryStream = new MemoryStream();

            logger.LogInformation($"Retrieving replay for score {scoreId}");

            using var response = await s3Client.GetObjectAsync(
                legacyScore ? getLegacyBucket(rulesetId) : AppSettings.S3ReplaysBucketName,
                getPathToReplay(scoreId));

            await response.ResponseStream.CopyToAsync(memoryStream);

            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }

        private static string getLegacyBucket(ushort rulesetId) =>
            string.Format(AppSettings.S3LegacyReplaysBucketName, LegacyRulesetHelper.GetRulesetNameFromLegacyId(rulesetId));

        private static string getPathToReplay(long scoreId) => scoreId.ToString(CultureInfo.InvariantCulture);
    }
}
