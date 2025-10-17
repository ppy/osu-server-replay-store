// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Server.ReplayStore.Configuration;
using osu.Server.ReplayStore.Helpers;

namespace osu.Server.ReplayStore.Services
{
    public class FileReplayCache : IReplayCache
    {
        private readonly string baseDirectory;
        private readonly string legacyBaseDirectory;

        public FileReplayCache(string? directory = null, string? legacyDirectory = null)
        {
            baseDirectory = directory ?? AppSettings.ReplayCacheStoragePath;
            legacyBaseDirectory = legacyDirectory ?? AppSettings.LegacyReplayCacheStoragePath;
        }

        public Task AddAsync(long scoreId, ushort rulesetId, bool legacyScore, byte[] replayData)
        {
            return File.WriteAllBytesAsync(
                getPathToReplay(scoreId, rulesetId, legacyScore),
                replayData);
        }

        public async Task<byte[]?> FindReplayDataAsync(long scoreId, ushort rulesetId, bool legacyScore)
        {
            string replayPath = getPathToReplay(scoreId, rulesetId, legacyScore);

            if (!File.Exists(replayPath))
                return null;

            byte[] replayData = await File.ReadAllBytesAsync(replayPath);

            return replayData;
        }

        public Task RemoveAsync(long scoreId, ushort rulesetId, bool legacyScore)
        {
            File.Delete(getPathToReplay(scoreId, rulesetId, legacyScore));

            return Task.CompletedTask;
        }

        private string getReplayDirectory(ushort rulesetId, bool legacyScore)
        {
            string date = DateTime.Today.ToString("ddMMyy");

            string baseCacheDirectory = legacyScore
                ? string.Format(legacyBaseDirectory, LegacyRulesetHelper.GetRulesetNameFromLegacyId(rulesetId))
                : baseDirectory;

            string datedDirectory = Path.Combine(baseCacheDirectory, date);

            if (!Directory.Exists(datedDirectory))
                Directory.CreateDirectory(datedDirectory);

            return datedDirectory;
        }

        private string getPathToReplay(long scoreId, ushort rulesetId, bool legacyScore) =>
            Path.Combine(getReplayDirectory(rulesetId, legacyScore), scoreId.ToString(CultureInfo.InvariantCulture));
    }
}
