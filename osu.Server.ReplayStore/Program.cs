using System.Net;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.AspNetCore.HttpLogging;
using osu.Server.ReplayStore.Configuration;
using osu.Server.ReplayStore.Services;
using StatsdClient;

namespace osu.Server.ReplayStore
{
    [UsedImplicitly]
    public class Program
    {
        public const string INTEGRATION_TEST_ENVIRONMENT = "IntegrationTest";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();

            builder.Services.AddHttpLogging(logging =>
            {
                logging.LoggingFields = HttpLoggingFields.All;
            });

            builder.Services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();

                if (AppSettings.SentryDsn != null)
                    logging.AddSentry();
            });

            switch (builder.Environment.EnvironmentName)
            {
                case "Development":
                {
                    builder.Services.AddSwaggerGen(c =>
                    {
                        c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, Assembly.GetExecutingAssembly().GetName().Name + ".xml"));
                    });
                    break;
                }

                case "Staging":
                {
                    if (AppSettings.SentryDsn == null)
                    {
                        throw new InvalidOperationException("SENTRY_DSN environment variable not set. "
                                                            + "Please set the value of this variable to a valid Sentry DSN to use for logging events.");
                    }

                    break;
                }

                case "Production":
                {
                    if (AppSettings.SentryDsn == null)
                    {
                        throw new InvalidOperationException("SENTRY_DSN environment variable not set. "
                                                            + "Please set the value of this variable to a valid Sentry DSN to use for logging events.");
                    }

                    if (AppSettings.DatadogAgentHost == null)
                    {
                        throw new InvalidOperationException("DD_AGENT_HOST environment variable not set. "
                                                            + "Please set the value of this variable to a valid hostname of a Datadog agent.");
                    }

                    break;
                }
            }

            switch (AppSettings.StorageType)
            {
                case StorageType.Local:
                    builder.Services.AddTransient<IReplayStorage, LocalReplayStorage>();
                    break;

                case StorageType.S3:
                    builder.Services.AddSingleton<IReplayStorage, S3ReplayStorage>();
                    break;

                default:
                {
                    if (builder.Environment.EnvironmentName == INTEGRATION_TEST_ENVIRONMENT)
                        break;

                    throw new InvalidOperationException($"REPLAY_STORAGE_TYPE environment variable not set to a valid value (`{AppSettings.StorageType}`). "
                                                        + "The variable is used to choose the implementation of replay storage used. "
                                                        + "Valid values are:\n"
                                                        + "- `local` (requires setting `LOCAL_REPLAY_STORAGE_PATH`),\n"
                                                        + "- `s3` (requires setting `S3_ACCESS_KEY`, `S3_SECRET_KEY`, `S3_REPLAY_BUCKET_{NAME,REGION}`)");
                }
            }

            if (AppSettings.SentryDsn != null)
            {
                builder.WebHost.UseSentry(options =>
                {
                    options.Environment = builder.Environment.EnvironmentName;
                    options.Dsn = AppSettings.SentryDsn;
                });
            }

            if (AppSettings.DatadogAgentHost != null)
            {
                DogStatsd.Configure(new StatsdConfig
                {
                    StatsdServerName = AppSettings.DatadogAgentHost,
                    Prefix = "osu.server.replay-cache",
                    ConstantTags = new[]
                    {
                        $@"hostname:{Dns.GetHostName()}",
                        $@"startup:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"
                    }
                });
            }

            if (builder.Environment.EnvironmentName != INTEGRATION_TEST_ENVIRONMENT)
            {
                builder.Services.AddTransient<IReplayCache, FileReplayCache>();
                builder.Services.AddHostedService<ExpireReplayCacheWorker>();
            }

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseReDoc();
            }

            app.MapControllers();
            app.UseHttpLogging();

            app.Run();
        }
    }
}