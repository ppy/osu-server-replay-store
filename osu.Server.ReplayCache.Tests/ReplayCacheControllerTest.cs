// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Net;
using Dapper;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using osu.Framework.Extensions;
using osu.Server.QueueProcessor;
using osu.Server.ReplayCache.Services;
using osu.Server.ReplayCache.Tests.Resources;

namespace osu.Server.ReplayCache.Tests
{
    public class ReplayCacheControllerTest : IntegrationTest
    {
        private const string solo_replay_filename = "solo-replay.osr";
        private const string legacy_replay_filename = "legacy-replay.osr";

        protected new HttpClient Client { get; }

        private readonly LocalReplayStorage replayStorage;
        private readonly IDistributedCache distributedCache;

        public ReplayCacheControllerTest(IntegrationTestWebApplicationFactory<Program> webApplicationFactory)
            : base(webApplicationFactory)
        {
            string tempPath = Path.GetTempPath();

            string legacyReplayDirectory = Path.Combine(tempPath, $"{nameof(ReplayCacheControllerTest)}_{0}");

            foreach (string ruleset in new[] { "osu", "taiko", "fruits", "mania" })
            {
                string directory = string.Format(legacyReplayDirectory, ruleset);
                Directory.CreateDirectory(directory);
            }

            replayStorage = new LocalReplayStorage(
                Directory.CreateTempSubdirectory(nameof(ReplayCacheControllerTest)).FullName,
                legacyReplayDirectory);

            Client = webApplicationFactory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddTransient<IReplayStorage>(_ => replayStorage);
                });
            }).CreateClient();

            distributedCache = webApplicationFactory.Services.GetRequiredService<IDistributedCache>();
        }

        [Fact]
        public async Task TestPutReplay_NewReplay()
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            await db.ExecuteAsync(
                "INSERT INTO `scores` (`id`, `user_id`, `ruleset_id`, `beatmap_id`, `data`, `ended_at`) values (1, 1, 0, 1, '{}', now());");

            using var stream = TestResources.GetResource(solo_replay_filename)!;

            var form = new MultipartFormDataContent();
            form.Add(new StreamContent(stream), "replayFile", solo_replay_filename);

            var response = await Client.PutAsync("/replays/1", form);
            Assert.True(response.IsSuccessStatusCode);

            byte[]? cachedReplay = await distributedCache.GetAsync("solo-replay-1");
            Assert.NotNull(cachedReplay);
        }

        [Fact]
        public async Task TestPutReplay_FailsIfNoScore()
        {
            using var stream = TestResources.GetResource(solo_replay_filename)!;

            var form = new MultipartFormDataContent();
            form.Add(new StreamContent(stream), "replayFile", solo_replay_filename);

            var response = await Client.PutAsync("/replays/1", form);
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task TestPutLegacyReplay_NewReplay()
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            await db.ExecuteAsync(
                "INSERT INTO `osu_scores_high` (`score_id`, `user_id`, `beatmap_id`) values (1, 1, 1);");

            await db.ExecuteAsync(
                "INSERT INTO `phpbb_users` (`user_id`, `username`, `username_clean`, `country_acronym`, `user_permissions`, `user_sig`, `user_occ`, `user_interests`) VALUES (1, 'test', 'test', 'JP', '', '', '', '')");

            await db.ExecuteAsync(
                "INSERT INTO `osu_replays` (`score_id`) VALUES (1);");

            await db.ExecuteAsync(
                "INSERT INTO `osu_beatmaps` (`beatmap_id`, `checksum`) VALUES (1, '5d370b1b0483f4fc7c64bff0ade06c0f');");

            using var stream = TestResources.GetResource(legacy_replay_filename)!;

            var form = new MultipartFormDataContent();
            form.Add(new StreamContent(stream), "replayFile", legacy_replay_filename);

            var response = await Client.PutAsync("/replays/0/1", form);
            Assert.True(response.IsSuccessStatusCode);

            byte[]? cachedReplay = await distributedCache.GetAsync("legacy-replay-0_1");
            Assert.NotNull(cachedReplay);
        }

        [Fact]
        public async Task TestPutLegacyReplay_FailsIfNoScore()
        {
            using var stream = TestResources.GetResource(legacy_replay_filename)!;

            var form = new MultipartFormDataContent();
            form.Add(new StreamContent(stream), "replayFile", legacy_replay_filename);

            var response = await Client.PutAsync("/replays/0/1", form);
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task TaskGetReplay_SendsReplay()
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            await db.ExecuteAsync(
                "INSERT INTO `scores` (`id`, `user_id`, `ruleset_id`, `beatmap_id`, `data`, `ended_at`, `has_replay`) values (1, 1, 0, 1, '{}', now(), 1);");

            using var stream = TestResources.GetResource(solo_replay_filename)!;

            await replayStorage.StoreReplayAsync(1, 0, false, stream);

            var response = await Client.GetAsync("/replays/1");
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal("0", response.Headers.GetValues("X-Cache-Hit").Single());
        }

        [Fact]
        public async Task TaskGetReplay_FailsIfNoScore()
        {
            var response = await Client.GetAsync("/replays/1");
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task TaskGetReplay_FailsIfNoReplay()
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            await db.ExecuteAsync(
                "INSERT INTO `scores` (`id`, `user_id`, `ruleset_id`, `beatmap_id`, `data`, `ended_at`, `has_replay`) values (1, 1, 0, 1, '{}', now(), 0);");

            var response = await Client.GetAsync("/replays/1");
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task TaskGetLegacyReplay_SendsReplay()
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            await db.ExecuteAsync(
                "INSERT INTO `osu_scores_high` (`score_id`, `user_id`, `beatmap_id`, `replay`) values (1, 1, 1, 1);");

            await db.ExecuteAsync(
                "INSERT INTO `phpbb_users` (`user_id`, `username`, `username_clean`, `country_acronym`, `user_permissions`, `user_sig`, `user_occ`, `user_interests`) VALUES (1, 'test', 'test', 'JP', '', '', '', '')");

            await db.ExecuteAsync(
                "INSERT INTO `osu_replays` (`score_id`) VALUES (1);");

            await db.ExecuteAsync(
                "INSERT INTO `osu_beatmaps` (`beatmap_id`, `checksum`) VALUES (1, '5d370b1b0483f4fc7c64bff0ade06c0f');");

            using var stream = TestResources.GetResource(legacy_replay_filename)!;

            await replayStorage.StoreReplayAsync(1, 0, true, stream);

            var response = await Client.GetAsync("/replays/0/1");
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal("0", response.Headers.GetValues("X-Cache-Hit").Single());
        }

        [Fact]
        public async Task TaskGetLegacyReplay_FailsIfNoScore()
        {
            var response = await Client.GetAsync("/replays/0/1");
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task TaskGetLegacyReplay_FailsIfNoReplay()
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            await db.ExecuteAsync(
                "INSERT INTO `osu_scores_high` (`score_id`, `user_id`, `beatmap_id`, `replay`) values (1, 1, 1, 0);");

            var response = await Client.GetAsync("/replays/0/1");
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task TestDeleteReplay_DeletesReplay()
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            await db.ExecuteAsync(
                "INSERT INTO `scores` (`id`, `user_id`, `ruleset_id`, `beatmap_id`, `data`, `ended_at`, `has_replay`) values (1, 1, 0, 1, '{}', now(), 1);");

            using var stream = TestResources.GetResource(solo_replay_filename)!;

            await replayStorage.StoreReplayAsync(1, 0, false, stream);
            await distributedCache.SetAsync("solo-replay-1", await stream.ReadAllBytesToArrayAsync());

            var response = await Client.DeleteAsync("/replays/1");
            Assert.True(response.IsSuccessStatusCode);

            byte[]? cachedReplay = await distributedCache.GetAsync("solo-replay-1");
            Assert.Null(cachedReplay);
        }

        [Fact]
        public async Task TaskDeleteReplay_FailsIfNoScore()
        {
            var response = await Client.DeleteAsync("/replays/1");
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task TaskDeleteReplay_FailsIfNoReplay()
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            await db.ExecuteAsync(
                "INSERT INTO `scores` (`id`, `user_id`, `ruleset_id`, `beatmap_id`, `data`, `ended_at`, `has_replay`) values (1, 1, 0, 1, '{}', now(), 0);");

            var response = await Client.DeleteAsync("/replays/1");
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task TestDeleteLegacyReplay_DeletesReplay()
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            await db.ExecuteAsync(
                "INSERT INTO `osu_scores_high` (`score_id`, `user_id`, `beatmap_id`, `replay`) values (1, 1, 1, 1);");

            using var stream = TestResources.GetResource(legacy_replay_filename)!;

            await replayStorage.StoreReplayAsync(1, 0, false, stream);
            await distributedCache.SetAsync("legacy-replay-0_1", await stream.ReadAllBytesToArrayAsync());

            var response = await Client.DeleteAsync("/replays/0/1");
            Assert.True(response.IsSuccessStatusCode);

            byte[]? cachedReplay = await distributedCache.GetAsync("legacy-replay-0_1");
            Assert.Null(cachedReplay);
        }

        [Fact]
        public async Task TaskDeleteLegacyReplay_FailsIfNoScore()
        {
            var response = await Client.DeleteAsync("/replays/0/1");
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task TaskDeleteLegacyReplay_FailsIfNoReplay()
        {
            using var db = await DatabaseAccess.GetConnectionAsync();

            await db.ExecuteAsync(
                "INSERT INTO `osu_scores_high` (`score_id`, `user_id`, `beatmap_id`, `replay`) values (1, 1, 1, 0);");

            var response = await Client.DeleteAsync("/replays/0/1");
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
