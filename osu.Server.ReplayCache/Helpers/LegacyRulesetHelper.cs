// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Server.ReplayCache.Helpers
{
    public static class LegacyRulesetHelper
    {
        public static string GetRulesetNameFromLegacyId(int legacyId)
        {
            switch (legacyId)
            {
                case 0:
                    return @"osu";

                case 1:
                    return @"taiko";

                case 2:
                    return @"fruits";

                case 3:
                    return @"mania";

                default:
                    throw new ArgumentException($"Invalid ruleset ID: {legacyId}", nameof(legacyId));
            }
        }

        public static string GetLegacyHighScoreTableFromLegacyId(int legacyId)
        {
            string tableSuffix = getLegacyTableSuffixFromLegacyId(legacyId);
            return $"osu_scores{tableSuffix}_high";
        }

        public static string GetLegacyReplayViewCountTableFromLegacyId(int legacyId)
        {
            string tableSuffix = getLegacyTableSuffixFromLegacyId(legacyId);
            return $"osu_replays{tableSuffix}";
        }

        private static string getLegacyTableSuffixFromLegacyId(int legacyId)
        {
            bool legacySuffix = legacyId != 0;

            string ruleset = GetRulesetNameFromLegacyId(legacyId);

            return legacySuffix ? $"_{ruleset}" : string.Empty;
        }
    }
}
