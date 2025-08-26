// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Server.ReplayStore.Services
{
    public interface IReplayStorage
    {
        Task StoreReplayAsync(long scoreId, ushort rulesetId, bool legacyScore, Stream replayData);

        Task<Stream> GetReplayStreamAsync(long scoreId, ushort rulesetId, bool legacyScore);

        Task DeleteReplayAsync(long scoreId, ushort rulesetId, bool legacyScore);
    }
}
