// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using osu.Framework.Extensions;
using osu.Server.QueueProcessor;
using osu.Server.ReplayStore.Helpers;
using osu.Server.ReplayStore.Models.Database;
using osu.Server.ReplayStore.Services;
using StatsdClient;

namespace osu.Server.ReplayStore
{
    [Route("replays")]
    public class ReplayStoreController : Controller
    {
        private const string content_type = "application/x-osu-replay";

        private readonly IReplayStorage replayStorage;
        private readonly IReplayCache replayCache;

        public ReplayStoreController(IReplayStorage replayStorage, IReplayCache replayCache)
        {
            this.replayStorage = replayStorage;
            this.replayCache = replayCache;
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
            Score? score;

            using (var db = await DatabaseAccess.GetConnectionAsync())
            {
                score = await db.GetScoreAsync(scoreId);
            }

            if (score == null)
                return NotFound();

            using var replayStream = replayFile.OpenReadStream();
            byte[] replayBytes = await replayStream.ReadAllRemainingBytesToArrayAsync();

            replayStream.Seek(0, SeekOrigin.Begin);

            await replayStorage.StoreReplayAsync(scoreId, score.ruleset_id, legacyScore: false, replayStream);
            await replayCache.AddAsync(scoreId, score.ruleset_id, legacyScore: false, replayBytes);

            DogStatsd.Increment("replays_uploaded", tags: ["type:lazer"]);
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
            HighScore? score;

            using (var db = await DatabaseAccess.GetConnectionAsync())
            {
                score = await db.GetLegacyScoreAsync(legacyScoreId, rulesetId);
            }

            if (score == null)
                return NotFound();

            using var replayStream = replayFile.OpenReadStream();
            byte[] replayBytes = await replayStream.ReadAllRemainingBytesToArrayAsync();

            replayStream.Seek(0, SeekOrigin.Begin);

            await replayStorage.StoreReplayAsync(legacyScoreId, rulesetId, legacyScore: true, replayStream);

            Stream replayWithHeaders;

            using (var db = await DatabaseAccess.GetConnectionAsync())
            {
                replayWithHeaders = await createLegacyReplayWithHeadersAsync(
                    replayBytes,
                    rulesetId,
                    score,
                    db);
            }

            await replayCache.AddAsync(
                legacyScoreId,
                rulesetId,
                legacyScore: true,
                await replayWithHeaders.ReadAllRemainingBytesToArrayAsync());

            await replayWithHeaders.DisposeAsync();

            DogStatsd.Increment("replays_uploaded", tags: ["type:legacy"]);
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
            Score? score;

            using (var db = await DatabaseAccess.GetConnectionAsync())
            {
                score = await db.GetScoreAsync(scoreId);
            }

            if (score == null || !score.has_replay)
                return NotFound();

            string fileName = createFileName(scoreId, score.beatmap_id, score.ruleset_id, legacyScore: false);

            byte[]? cachedReplay = await replayCache.FindReplayDataAsync(scoreId, score.ruleset_id, legacyScore: false);

            if (cachedReplay != null)
            {
                DogStatsd.Increment("replays_downloaded", tags: ["type:lazer", "source:cache"]);

                Response.Headers.Append("X-Cache-Hit", "1");
                return File(cachedReplay, content_type, fileName);
            }

            var replayStream = await replayStorage.GetReplayStreamAsync(scoreId, score.ruleset_id, legacyScore: false);

            DogStatsd.Increment("replays_downloaded", tags: ["type:lazer", "source:storage"]);

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
            HighScore? score;

            using (var db = await DatabaseAccess.GetConnectionAsync())
            {
                score = await db.GetLegacyScoreAsync(legacyScoreId, rulesetId);
            }

            if (score == null || !score.replay)
                return NotFound();

            string fileName = createFileName(legacyScoreId, (uint)score.beatmap_id, rulesetId, legacyScore: true);

            byte[]? cachedReplay = await replayCache.FindReplayDataAsync(legacyScoreId, rulesetId, legacyScore: true);

            if (cachedReplay != null)
            {
                DogStatsd.Increment("replays_downloaded", tags: ["type:legacy", "source:cache"]);

                Response.Headers.Append("X-Cache-Hit", "1");
                return File(cachedReplay, content_type, fileName);
            }

            using var replayStream = await replayStorage.GetReplayStreamAsync(legacyScoreId, rulesetId, legacyScore: true);

            Stream replayWithHeaders;

            using (var db = await DatabaseAccess.GetConnectionAsync())
            {
                replayWithHeaders = await createLegacyReplayWithHeadersAsync(
                    await replayStream.ReadAllRemainingBytesToArrayAsync(),
                    rulesetId,
                    score,
                    db);
            }

            DogStatsd.Increment("replays_downloaded", tags: ["type:legacy", "source:storage"]);

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
            Score? score;

            using (var db = await DatabaseAccess.GetConnectionAsync())
            {
                score = await db.GetScoreAsync(scoreId);
            }

            if (score == null || !score.has_replay)
                return NotFound();

            await replayStorage.DeleteReplayAsync(scoreId, score.ruleset_id, legacyScore: false);
            await replayCache.RemoveAsync(scoreId, score.ruleset_id, legacyScore: false);

            DogStatsd.Increment("replays_deleted", tags: ["type:lazer"]);

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
            HighScore? score;

            using (var db = await DatabaseAccess.GetConnectionAsync())
            {
                score = await db.GetLegacyScoreAsync(legacyScoreId, rulesetId);
            }

            if (score == null || !score.replay)
                return NotFound();

            await replayStorage.DeleteReplayAsync(legacyScoreId, rulesetId, legacyScore: true);
            await replayCache.RemoveAsync(legacyScoreId, rulesetId, legacyScore: true);

            DogStatsd.Increment("replays_deleted", tags: ["type:legacy"]);

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
    }
}
