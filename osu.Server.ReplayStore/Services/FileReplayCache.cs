// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Server.ReplayStore.Configuration;
using osu.Server.ReplayStore.Helpers;
using StackExchange.Redis;

namespace osu.Server.ReplayStore.Services
{
    public class FileReplayCache : IReplayCache
    {
        private readonly IConnectionMultiplexer connectionMultiplexer;
        private readonly string baseDirectory;
        private readonly string legacyBaseDirectory;

        public FileReplayCache(IConnectionMultiplexer connectionMultiplexer, string? directory = null, string? legacyDirectory = null)
        {
            this.connectionMultiplexer = connectionMultiplexer;
            baseDirectory = directory ?? AppSettings.ReplayCacheStoragePath;
            legacyBaseDirectory = legacyDirectory ?? AppSettings.LegacyReplayCacheStoragePath;
        }

        public async Task AddAsync(long scoreId, ushort rulesetId, bool legacyScore, byte[] replayData)
        {
            await File.WriteAllBytesAsync(
                getPathToReplay(scoreId, rulesetId, legacyScore),
                replayData);

            var db = connectionMultiplexer.GetDatabase();

            await db.StringSetAsync(
                getCacheKey(scoreId, rulesetId, legacyScore),
                1, // filler value
                expiry: TimeSpan.FromHours(AppSettings.ReplayCacheHours));
        }

        public async Task<byte[]?> FindReplayDataAsync(long scoreId, ushort rulesetId, bool legacyScore)
        {
            var db = connectionMultiplexer.GetDatabase();

            if (!await db.KeyExistsAsync(getCacheKey(scoreId, rulesetId, legacyScore)))
                return null;

            byte[] replayData = await File.ReadAllBytesAsync(getPathToReplay(scoreId, rulesetId, legacyScore));

            return replayData;
        }

        public async Task RemoveAsync(long scoreId, ushort rulesetId, bool legacyScore)
        {
            var db = connectionMultiplexer.GetDatabase();

            await db.KeyDeleteAsync(
                getCacheKey(scoreId, rulesetId, legacyScore));

            File.Delete(getPathToReplay(scoreId, rulesetId, legacyScore));
        }

        private string getReplayDirectory(ushort rulesetId, bool legacyScore) =>
            legacyScore
                ? string.Format(legacyBaseDirectory, LegacyRulesetHelper.GetRulesetNameFromLegacyId(rulesetId))
                : baseDirectory;

        private string getPathToReplay(long scoreId, ushort rulesetId, bool legacyScore) =>
            Path.Combine(getReplayDirectory(rulesetId, legacyScore), scoreId.ToString(CultureInfo.InvariantCulture));

        private static string getCacheKey(long scoreId, ushort rulesetId, bool legacyScore) =>
            legacyScore
                ? $"legacy-replay-{rulesetId}_{scoreId}"
                : $"solo-replay-{rulesetId}_{scoreId}";
    }
}
