// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace osu.Server.ReplayStore.Tests
{
    public class IntegrationTestWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram>
        where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            // this is a non-standard environment string (usually it's one of "Development", "Staging", or "Production").
            // this is primarily done such that integration tests that use this factory have full control over dependency injection.
            builder.UseEnvironment(Program.INTEGRATION_TEST_ENVIRONMENT);
        }
    }
}
