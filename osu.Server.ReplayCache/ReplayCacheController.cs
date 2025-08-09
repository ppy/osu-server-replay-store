// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using MySqlConnector;
using osu.Framework.Extensions;
using osu.Server.QueueProcessor;
using osu.Server.ReplayCache.Helpers;
using osu.Server.ReplayCache.Models.Database;
using osu.Server.ReplayCache.Services;
using StatsdClient;

namespace osu.Server.ReplayCache
{
    [Route("replays")]
    public class ReplayCacheController : Controller
    {
        private const string content_type = "application/x-osu-replay";

        private readonly IReplayStorage replayStorage;
        private readonly IDistributedCache distributedCache;

        public ReplayCacheController(IReplayStorage replayStorage, IDistributedCache distributedCache)
        {
            this.replayStorage = replayStorage;
            this.distributedCache = distributedCache;
        }

        /// <summary>
        /// Uploads a new solo replay.
        /// </summary>
        /// <response code="204">The replay was uploaded successfully.</response>
        /// <response code="404">The given score ID could not be found in the database.</response>
        [HttpPut]
        [Route("{scoreId:long}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> PutReplayAsync(
            [FromRoute] long scoreId,
            IFormFile replayFile)
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            var score = await db.GetScoreAsync(scoreId);

            if (score == null)
                return NotFound();

            using var replayStream = replayFile.OpenReadStream();

            using var memoryStream = new MemoryStream();
            await replayStream.CopyToAsync(memoryStream);

            byte[] replayBytes = memoryStream.ToArray();

            await replayStorage.StoreReplayAsync(scoreId, score.ruleset_id, legacyScore: false, replayStream);

            await distributedCache.SetAsync(
                getCacheKey(scoreId, score.ruleset_id, legacyScore: false),
                replayBytes,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1),
                });

            DogStatsd.Increment("replays_uploaded");
            return NoContent();
        }

        /// <summary>
        /// Uploads a new legacy replay.
        /// </summary>
        /// <response code="204">The replay was uploaded successfully.</response>
        /// <response code="404">The given score ID could not be found in the database.</response>
        [HttpPut]
        [Route("{rulesetId:int}/{legacyScoreId:long}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> PutLegacyReplayAsync(
            [FromRoute] ushort rulesetId,
            [FromRoute] long legacyScoreId,
            IFormFile replayFile)
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            var score = await db.GetLegacyScoreAsync(legacyScoreId, rulesetId);

            if (score == null)
                return NotFound();

            using var replayStream = replayFile.OpenReadStream();

            using var memoryStream = new MemoryStream();
            await replayStream.CopyToAsync(memoryStream);

            byte[] replayBytes = memoryStream.ToArray();

            await replayStorage.StoreReplayAsync(legacyScoreId, rulesetId, legacyScore: true, replayStream);

            using var replayWithHeaders = await createLegacyReplayWithHeadersAsync(
                replayBytes,
                rulesetId,
                score,
                db);

            await distributedCache.SetAsync(
                getCacheKey(legacyScoreId, rulesetId, legacyScore: true),
                await replayWithHeaders.ReadAllRemainingBytesToArrayAsync(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1),
                });

            DogStatsd.Increment("replays_uploaded", tags: ["legacy"]);
            return NoContent();
        }

        /// <summary>
        /// Fetches the solo replay for a score.
        /// </summary>
        /// <response code="200">The replay was downloaded successfully.</response>
        /// <response code="404">The given score ID could not be found in the database, or the score has no replay.</response>
        [HttpGet]
        [Route("{scoreId:long}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [Produces(content_type)]
        public async Task<IActionResult> GetReplayAsync([FromRoute] long scoreId)
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            var score = await db.GetScoreAsync(scoreId);

            if (score == null || !score.has_replay)
                return NotFound();

            string fileName = createFileName(scoreId, score.beatmap_id, score.ruleset_id, legacyScore: false);

            byte[]? cachedReplay = await distributedCache.GetAsync(getCacheKey(scoreId, score.ruleset_id, legacyScore: false));

            if (cachedReplay != null)
            {
                DogStatsd.Increment("replays_downloaded", tags: ["cache"]);

                Response.Headers.Append("X-Cache-Hit", "1");
                return File(cachedReplay, content_type, fileName);
            }

            var replayStream = await replayStorage.GetReplayStreamAsync(scoreId, score.ruleset_id, legacyScore: false);

            DogStatsd.Increment("replays_downloaded");

            Response.Headers.Append("X-Cache-Hit", "0");
            return File(replayStream, content_type, fileName);
        }

        /// <summary>
        /// Fetches the replay for a legacy score.
        /// </summary>
        /// <response code="200">The replay was downloaded successfully.</response>
        /// <response code="404">The given score ID could not be found in the database, or the score has no replay.</response>
        [HttpGet]
        [Route("{rulesetId:int}/{legacyScoreId:long}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [Produces(content_type)]
        public async Task<IActionResult> GetLegacyReplayAsync(
            [FromRoute] ushort rulesetId,
            [FromRoute] long legacyScoreId)
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            var score = await db.GetLegacyScoreAsync(legacyScoreId, rulesetId);

            if (score == null || !score.replay)
                return NotFound();

            string fileName = createFileName(legacyScoreId, (uint)score.beatmap_id, rulesetId, legacyScore: true);

            byte[]? cachedReplay = await distributedCache.GetAsync(getCacheKey(legacyScoreId, rulesetId, legacyScore: true));

            if (cachedReplay != null)
            {
                DogStatsd.Increment("replays_downloaded", tags: ["cache", "legacy"]);

                Response.Headers.Append("X-Cache-Hit", "1");
                return File(cachedReplay, content_type, fileName);
            }

            using var replayStream = await replayStorage.GetReplayStreamAsync(legacyScoreId, rulesetId, legacyScore: true);

            var replayWithHeaders = await createLegacyReplayWithHeadersAsync(
                await replayStream.ReadAllRemainingBytesToArrayAsync(),
                rulesetId,
                score,
                db);

            DogStatsd.Increment("replays_downloaded", tags: ["legacy"]);

            Response.Headers.Append("X-Cache-Hit", "0");
            return File(replayWithHeaders, content_type, fileName);
        }

        /// <summary>
        /// Deletes the solo replay for a score.
        /// </summary>
        /// <response code="204">The replay was deleted successfully.</response>
        /// <response code="404">The given score ID could not be found in the database, or the score has no replay.</response>
        [HttpDelete]
        [Route("{scoreId:long}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteReplayAsync([FromRoute] long scoreId)
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            var score = await db.GetScoreAsync(scoreId);

            if (score == null || !score.has_replay)
                return NotFound();

            await replayStorage.DeleteReplayAsync(scoreId, score.ruleset_id, legacyScore: false);
            await distributedCache.RemoveAsync(getCacheKey(scoreId, score.ruleset_id, legacyScore: false));

            DogStatsd.Increment("replays_deleted");

            return NoContent();
        }

        /// <summary>
        /// Deletes the replay for a legacy score.
        /// </summary>
        /// <response code="204">The replay was deleted successfully.</response>
        /// <response code="404">The given score ID could not be found in the database, or the score has no replay.</response>
        [HttpDelete]
        [Route("{rulesetId:int}/{legacyScoreId:long}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteLegacyReplayAsync(
            [FromRoute] ushort rulesetId,
            [FromRoute] long legacyScoreId)
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            var score = await db.GetLegacyScoreAsync(legacyScoreId, rulesetId);

            if (score == null || !score.replay)
                return NotFound();

            await replayStorage.DeleteReplayAsync(legacyScoreId, rulesetId, legacyScore: true);
            await distributedCache.RemoveAsync(getCacheKey(legacyScoreId, rulesetId, legacyScore: true));

            DogStatsd.Increment("replays_deleted", tags: ["legacy"]);

            return NoContent();
        }

        private static async Task<Stream> createLegacyReplayWithHeadersAsync(byte[] frames, ushort rulesetId, HighScore legacyScore, MySqlConnection db)
        {
            var user = await db.GetUserAsync(legacyScore.user_id);
            Debug.Assert(user != null);

            var beatmap = await db.GetBeatmapAsync(legacyScore.beatmap_id);
            Debug.Assert(beatmap != null);

            int? scoreVersion = await db.GetLegacyScoreVersionAsync(legacyScore.score_id, rulesetId);

            var replayWithHeaders = LegacyReplayHelper.WriteReplayWithHeader(
                frames,
                rulesetId,
                scoreVersion,
                legacyScore,
                user,
                beatmap);

            return replayWithHeaders;
        }

        private static string createFileName(long scoreId, uint beatmapId, ushort rulesetId, bool legacyScore)
        {
            string ruleset = LegacyRulesetHelper.GetRulesetNameFromLegacyId(rulesetId);

            string replayType = legacyScore ? "replay" : "solo-replay";

            return $"{replayType}-{ruleset}_{beatmapId}_{scoreId}.osr";
        }

        private static string getCacheKey(long scoreId, ushort rulesetId, bool legacyScore) =>
            legacyScore
                ? $"legacy-replay-{rulesetId}_{scoreId}"
                : $"solo-replay-{scoreId}";
    }
}
