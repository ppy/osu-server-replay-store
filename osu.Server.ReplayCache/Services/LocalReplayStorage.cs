// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Server.ReplayCache.Configuration;
using osu.Server.ReplayCache.Helpers;

namespace osu.Server.ReplayCache.Services
{
    public class LocalReplayStorage : IReplayStorage
    {
        private readonly string baseDirectory;
        private readonly string legacyBaseDirectory;

        public LocalReplayStorage(string? directory = null, string? legacyDirectory = null)
        {
            baseDirectory = directory ?? AppSettings.LocalReplayStoragePath;
            legacyBaseDirectory = legacyDirectory ?? AppSettings.LocalLegacyReplayStoragePath;
        }

        public async Task StoreReplayAsync(long scoreId, ushort rulesetId, bool legacyScore, Stream replayData)
        {
            string path = getPathToReplay(scoreId, rulesetId, legacyScore);

            using var fileStream = File.OpenWrite(path);
            await replayData.CopyToAsync(fileStream);
        }

        public async Task<Stream> GetReplayStreamAsync(long scoreId, ushort rulesetId, bool legacyScore)
        {
            string path = getPathToReplay(scoreId, rulesetId, legacyScore);

            var memoryStream = new MemoryStream();

            using var fileStream = File.OpenRead(path);
            await fileStream.CopyToAsync(memoryStream);

            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }

        public Task DeleteReplayAsync(long scoreId, ushort rulesetId, bool legacyScore)
        {
            string path = getPathToReplay(scoreId, rulesetId, legacyScore);

            File.Delete(path);
            return Task.CompletedTask;
        }

        private string getReplayDirectory(ushort rulesetId, bool legacyScore) =>
            legacyScore
                ? string.Format(legacyBaseDirectory, LegacyRulesetHelper.GetRulesetNameFromLegacyId(rulesetId))
                : baseDirectory;

        private string getPathToReplay(long scoreId, ushort rulesetId, bool legacyScore) =>
            Path.Combine(getReplayDirectory(rulesetId, legacyScore), scoreId.ToString(CultureInfo.InvariantCulture));
    }
}
