using System.Threading.RateLimiting;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using NewsReporter.Abstracts;
using NewsReporter.Settings;
using NewsReporter.Utilities;
using Polly;
using Scalar.AspNetCore;

namespace NewsReporter;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        RegisterServices(builder);

        var app = builder.Build();

        // Register ScalarUI in development mode
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference(o => o.DisableTelemetry().DisableAgent().DisableMcp()); // Default path acts as: /scalar/v1
            app.MapGet("/", () => Results.Redirect("/scalar/v1"));
        }

        // Register our API controller
        app.MapControllers();

        // Add a last-chance logger to catch and log any fatal unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var exception = args.ExceptionObject as Exception;
            app.Logger.LogCritical(exception, "Unhandled exception - application terminating");
        };
        app.Run();
    }

    private static void RegisterServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        // Register our configuration
        services.Configure<CachingSettings>(configuration.GetSection(CachingSettings.SectionName));
        services.Configure<TargetApiSettings>(configuration.GetSection(TargetApiSettings.SectionName));

        // Some configuration for routing - force lowercase urls for consistency
        services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

        // Default console logging - could extend here with Serilog (or any other logging framework) if desired
        services.AddLogging();

        // Add a HybridCache for in-memory caching.  Could be extended to use a distributed/persisted cache (eg Redis) if desired to share between different instances of the NewsReporter service.
        // Note that the HybridCache manages concurrent requests to create missing values so that multiple concurrent requests for the same key will result in only a single call to the underlying factory
        services.AddHybridCache();

        // Register a wrapper for the HybridCache to allow us to use our own ICache interface for testing/mocking purposes
        services.AddSingleton<ICache>(sp => new HybridCacheWrapper(sp.GetRequiredService<HybridCache>()));

        // Register options for and a HttpClient for use by the HackerNewsFetcher.  Note we also configure a rate limiter to ensure that we don't call the target API excessively
        services.AddOptions<HttpClientFactoryOptions>()
            .Configure<IOptions<TargetApiSettings>>((options, targetApiSettings)
                => options.HttpClientActions.Add(client =>
                    {
                        client.BaseAddress = new Uri(targetApiSettings.Value.BaseUrl);
                        // We could add specific custom headers here if required to identify our service to the target API.
                        client.DefaultRequestHeaders.Add("Accept", "application/json");
                        client.Timeout = TimeSpan.FromSeconds(targetApiSettings.Value.TimeoutSeconds);
                    }));
        services.AddHttpClient<IFetcher, HackerNewsFetcher>()
        // Using the Resilience package, ensure that we limit the number of concurrent requests to the target API
            .AddResilienceHandler("FetcherRateLimiting", (builder, context) =>
            {
                var targetApiSettings = context.ServiceProvider.GetRequiredService<IOptions<TargetApiSettings>>().Value;
                var concurrencyOptions = new ConcurrencyLimiterOptions
                {
                    PermitLimit = targetApiSettings.MaxConcurrentRequests, // Allow n concurrent requests
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = targetApiSettings.MaxQueuedRequests // Allow n queued requests once concurrent limit is reached.
                };
                builder.AddConcurrencyLimiter(concurrencyOptions);
            });

        // Register our service types
        services.AddSingleton<IFetcher, HackerNewsFetcher>();
        services.AddSingleton<IAggregator, StoryAggregator>();

        // Register our controllers
        services.AddControllers();
        // Register Swagger for API documentation
        services.AddOpenApi();
    }
}
