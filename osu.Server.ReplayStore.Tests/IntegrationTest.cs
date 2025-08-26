// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using Dapper;
using osu.Server.QueueProcessor;

namespace osu.Server.ReplayStore.Tests
{
    [Collection("Integration Tests")] // ensures sequential execution
    public abstract class IntegrationTest : IClassFixture<IntegrationTestWebApplicationFactory<Program>>, IDisposable
    {
        protected readonly HttpClient Client;

        protected CancellationToken CancellationToken => cancellationSource.Token;
        private readonly CancellationTokenSource cancellationSource;

        protected IntegrationTest(IntegrationTestWebApplicationFactory<Program> webAppFactory)
        {
            Client = webAppFactory.CreateClient();
            reinitialiseDatabase();

            cancellationSource = Debugger.IsAttached
                ? new CancellationTokenSource()
                : new CancellationTokenSource(20000);

            emptyRedisCacheKeys();
        }

        private void reinitialiseDatabase()
        {
            using var db = DatabaseAccess.GetConnection();

            // just a safety measure for now to ensure we don't hit production.
            // will throw if not on test database.
            if (db.QueryFirstOrDefault<int?>("SELECT `count` FROM `osu_counts` WHERE name = 'is_production'") != null)
                throw new InvalidOperationException("You have just attempted to run tests on production and wipe data. Rethink your life decisions.");

            db.Execute("TRUNCATE TABLE `phpbb_users`");
            db.Execute("TRUNCATE TABLE `osu_beatmaps`");
            db.Execute("TRUNCATE TABLE `scores`");
            db.Execute("TRUNCATE TABLE `osu_scores_high`");
            db.Execute("TRUNCATE TABLE `osu_scores_taiko_high`");
            db.Execute("TRUNCATE TABLE `osu_scores_fruits_high`");
            db.Execute("TRUNCATE TABLE `osu_scores_mania_high`");
            db.Execute("TRUNCATE TABLE `osu_replays`");
            db.Execute("TRUNCATE TABLE `osu_replays_taiko`");
            db.Execute("TRUNCATE TABLE `osu_replays_fruits`");
            db.Execute("TRUNCATE TABLE `osu_replays_mania`");
        }

        private void emptyRedisCacheKeys()
        {
            using var redisConnection = RedisAccess.GetConnection();

            var endpoint = redisConnection.GetEndPoints()[0];
            var redisServer = redisConnection.GetServer(endpoint);

            var database = redisConnection.GetDatabase();

            foreach (var key in redisServer.Keys(pattern: "solo-replay*"))
                database.KeyDelete(key);

            foreach (var key in redisServer.Keys(pattern: "legacy-replay*"))
                database.KeyDelete(key);
        }

        public virtual void Dispose()
        {
            Client.Dispose();
            cancellationSource.Dispose();
        }
    }
}
