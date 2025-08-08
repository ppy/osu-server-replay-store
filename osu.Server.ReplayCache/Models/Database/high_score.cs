// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// ReSharper disable InconsistentNaming

namespace osu.Server.ReplayCache.Models.Database
{
    public class high_score
    {
        public ulong score_id { get; set; }

        public int beatmap_id { get; set; }

        public int user_id { get; set; }

        public int score { get; set; }

        public ushort maxcombo { get; set; }

        public string rank { get; set; } = null!;

        public ushort count50 { get; set; }

        public ushort count100 { get; set; }

        public ushort count300 { get; set; }

        public ushort countmiss { get; set; }

        public ushort countgeki { get; set; }

        public ushort countkatu { get; set; }

        public bool perfect { get; set; }

        public int enabled_mods { get; set; }

        public DateTimeOffset date { get; set; }

        public bool replay { get; set; }
    }
}
