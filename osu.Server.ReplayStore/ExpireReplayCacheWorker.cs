// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Server.ReplayStore.Configuration;
using osu.Server.ReplayStore.Services;
using StackExchange.Redis;

namespace osu.Server.ReplayStore
{
    public class ExpireReplayCacheWorker : BackgroundService
    {
        // expire = manual expiration via the EXPIRE cmd
        // expired = key with an expiry has elapsed its duration
        // evicted = key that was evicted due to memory, we should remove these as we won't be able to track them
        private static readonly string[] expired_events = ["expire", "expired", "evicted"];

        private readonly IConnectionMultiplexer connectionMultiplexer;
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly ILogger<ExpireReplayCacheWorker> logger;

        private IReplayCache replayCache = null!;

        public ExpireReplayCacheWorker(IConnectionMultiplexer connectionMultiplexer, IServiceScopeFactory serviceScopeFactory, ILogger<ExpireReplayCacheWorker> logger)
        {
            this.connectionMultiplexer = connectionMultiplexer;
            this.serviceScopeFactory = serviceScopeFactory;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = serviceScopeFactory.CreateScope();
            replayCache = scope.ServiceProvider.GetRequiredService<IReplayCache>();

            var subscriber = connectionMultiplexer.GetSubscriber();

            await subscriber.SubscribeAsync($"__keyspace@{AppSettings.RedisDatabase}__:*", handleKeyspaceEvent);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }

            await subscriber.UnsubscribeAllAsync();
        }

        private void handleKeyspaceEvent(RedisChannel channel, RedisValue type)
        {
            try
            {
                string eventType = type.ToString().ToLowerInvariant();

                if (!expired_events.Contains(eventType))
                    return;

                string key = extractKeyFromChannel(channel!);

                (long scoreId, ushort rulesetId, bool legacyScore) = extractDataFromKey(key);

                replayCache.RemoveAsync(scoreId, rulesetId, legacyScore).Wait();
            }
            catch (Exception ex)
            {
                // Due to this method being called by a Redis subscriber, exceptions fail to be raised properly.
                // Log the error and capture it to Sentry so that we don't ignore them.
                logger.LogError(ex, "Failed to process keyspace event");
                SentrySdk.CaptureException(ex);
            }
        }

        private static (long, ushort, bool) extractDataFromKey(string key)
        {
            string[] parts = key.Split('-');

            bool legacyScore = parts[0] == "legacy";

            string[] scoreAndRuleset = parts[2].Split('_');

            ushort rulesetId = ushort.Parse(scoreAndRuleset[0]);
            long scoreId = long.Parse(scoreAndRuleset[1]);

            return (scoreId, rulesetId, legacyScore);
        }

        private static string extractKeyFromChannel(string channel)
        {
            int index = channel.IndexOf(':');

            if (index >= 0 && index < channel.Length - 1)
            {
                return channel[(index + 1)..];
            }

            return channel;
        }
    }
}
