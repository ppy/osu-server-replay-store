// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions;
using osu.Game.IO.Legacy;
using osu.Server.ReplayCache.Models.Database;

namespace osu.Server.ReplayCache.Helpers
{
    public static class LegacyReplayHelper
    {
        private const int default_replay_version = 20151228;

        public static Stream WriteReplayWithHeader(byte[] frameData, ushort rulesetId, int? scoreVersion, HighScore score, User user, OsuBeatmap beatmap)
        {
            var memoryStream = new MemoryStream();

            using var writer = new SerializationWriter(memoryStream);

            string scoreChecksum = $"{score.maxcombo}osu{user.username}{beatmap.checksum}{score.score}{score.rank}";

            // header section
            writer.Write((byte)rulesetId);
            writer.Write(scoreVersion ?? default_replay_version);
            writer.Write(beatmap.checksum);
            writer.Write(user.username);
            writer.Write(scoreChecksum.ComputeMD5Hash());
            writer.Write(score.count300);
            writer.Write(score.count100);
            writer.Write(score.count50);
            writer.Write(score.countgeki);
            writer.Write(score.countkatu);
            writer.Write(score.countmiss);
            writer.Write(score.score);
            writer.Write(score.maxcombo);
            writer.Write(score.perfect);
            writer.Write(score.enabled_mods);

            writer.Write(string.Empty); // empty hp bar
            writer.Write(score.date.DateTime);

            writer.WriteByteArray(frameData);
            writer.Write(score.score_id);

            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }
    }
}
