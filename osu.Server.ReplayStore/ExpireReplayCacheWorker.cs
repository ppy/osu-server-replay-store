// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Server.ReplayStore.Configuration;

namespace osu.Server.ReplayStore
{
    public class ExpireReplayCacheWorker : BackgroundService
    {
        private readonly ILogger<ExpireReplayCacheWorker> logger;

        public ExpireReplayCacheWorker(ILogger<ExpireReplayCacheWorker> logger)
        {
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                removeExpiredDirectories();
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private void removeExpiredDirectories()
        {
            foreach (string replayFolder in getCacheFolders())
            {
                foreach (string cacheFolder in Directory.EnumerateDirectories(replayFolder))
                {
                    string cacheDate = Path.GetFileName(cacheFolder);

                    if ((DateTime.Today - getDateFromString(cacheDate)).TotalDays <= AppSettings.ReplayCacheDays)
                        continue;

                    Directory.Delete(cacheFolder, true);
                    logger.LogInformation("Deleted expired cache folder {CacheFolder}", cacheFolder);
                }
            }
        }

        private static DateTime getDateFromString(string date)
            => DateTime.ParseExact(date, "ddMMyy", null).Date;

        private static IEnumerable<string> getCacheFolders()
        {
            yield return AppSettings.ReplayCacheStoragePath;

            foreach (string ruleset in new[] { "osu", "taiko", "catch", "mania" })
                yield return string.Format(AppSettings.LegacyReplayCacheStoragePath, ruleset);
        }
    }
}
