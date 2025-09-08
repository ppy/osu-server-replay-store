// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Server.ReplayStore.Services
{
    public interface IReplayCache
    {
        Task AddAsync(long scoreId, ushort rulesetId, bool legacyScore, byte[] replayData);

        Task<byte[]?> FindReplayDataAsync(long scoreId, ushort rulesetId, bool legacyScore);

        Task RemoveAsync(long scoreId, ushort rulesetId, bool legacyScore);
    }
}
