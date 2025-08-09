// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps;
using osu.Server.ReplayCache.Helpers;
using osu.Server.ReplayCache.Models.Database;
using osu.Server.ReplayCache.Tests.Resources;

namespace osu.Server.ReplayCache.Tests
{
    public class LegacyReplayHelperTest
    {
        private const string legacy_replay_filename = "legacy-replay.osr";
        private const int legacy_replay_version = 20250628;

        [Fact]
        public async Task WriteReplayWithHeader_WritesValidHeader()
        {
            using var stream = TestResources.GetResource(legacy_replay_filename)!;

            var highScore = new HighScore
            {
                score_id = 4501250208,
                score = 13160096,
                maxcombo = 724,
                count50 = 0,
                count100 = 3,
                count300 = 525,
                countmiss = 0,
                countkatu = 3,
                countgeki = 105,
                perfect = true,
                enabled_mods = 64,
                user_id = 11315329,
                date = new DateTimeOffset(2023, 09, 04, 21, 10, 42, TimeSpan.Zero),
                rank = "S",
                replay = true,
            };

            var user = new User
            {
                username = "tsunyoku"
            };

            var beatmap = new OsuBeatmap
            {
                checksum = "5d370b1b0483f4fc7c64bff0ade06c0f"
            };

            var response = LegacyReplayHelper.WriteReplayWithHeader(
                await stream.ReadAllBytesToArrayAsync(),
                rulesetId: 0,
                legacy_replay_version,
                highScore,
                user,
                beatmap);

            var scoreDecoder = new TestLegacyScoreDecoder();

            var score = scoreDecoder.Parse(response);

            Assert.Equal(user.username, score.ScoreInfo.RealmUser.Username);
            Assert.Equal(highScore.count300, score.ScoreInfo.GetCount300());
            Assert.Equal(highScore.count100, score.ScoreInfo.GetCount100());
            Assert.Equal(highScore.count50, score.ScoreInfo.GetCount50());
            Assert.Equal(highScore.countmiss, score.ScoreInfo.GetCountMiss());
            Assert.Equal(highScore.score, score.ScoreInfo.LegacyTotalScore);
            Assert.Equal(highScore.maxcombo, score.ScoreInfo.MaxCombo);
            Assert.Equal(highScore.date.DateTime, score.ScoreInfo.Date);
            Assert.Equal(highScore.score_id, (ulong)score.ScoreInfo.LegacyOnlineID);
        }
    }

    public class TestLegacyScoreDecoder : LegacyScoreDecoder
    {
        protected override Ruleset GetRuleset(int rulesetId) => new OsuRuleset();

        protected override WorkingBeatmap GetBeatmap(string md5Hash) => new TestWorkingBeatmap(new Beatmap
        {
            BeatmapInfo = new BeatmapInfo
            {
                MD5Hash = md5Hash,
                Ruleset = new OsuRuleset().RulesetInfo,
                Difficulty = new BeatmapDifficulty(),
            },
            // needs to have at least one object so that `StandardisedScoreMigrationTools` doesn't die
            // when trying to recompute total score.
            HitObjects =
            {
                new HitCircle()
            },
            BeatmapVersion = 14,
        });
    }
}
