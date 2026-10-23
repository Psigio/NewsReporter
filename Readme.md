# News Reporter

Provides an asp.net RESTful interface to retrieve the top `n` news stories from HackerNews, using the publicly available API described at https://github.com/HackerNews/API.  The service retrieves the list of best stories from the HackerNews API lazily and caches it in memory for a period of time.  It retrieves a number of story details from the HackerNews API as requested and caches them in memory also for a period of time.  Calls made to the HackerNews API are rate-limited to ensure that the upstream API is not overloaded.

# Building
This code was built using .net 10 (version 10.0.112) and will operate on any version with a feature version equal to or higher than this.  To run locally, follow the steps below, assuming that you already have a working .net-10 SDK installed and network access is available to retrieve NuGet packages (by default this will be [nuget.org](https://www.nuget.org), but may be overriden by your own user/machine level nuget settings to point to a proxy or private repository.

1. Check out the repository.
2. In a console, go to the root directory and run `dotnet restore`, followed by `dotnet build` to build the application (`build` will typically call the restore step automatically, so you may omit the restore step if you like).
3. To verify the unit tests pass run `dotnet test`.
4. Once the tests pass, you can run `dotnet run --project NewsReporter`.
5. You may then visit the [Scalar UI](https://scalar.com/products/docs) running on [localhost](http://localhost:5000/scalar/v1#tag/news/GET/api/v1/news/beststories/{bestStoryCount}) and use the "Test Request" button to invoke the call.  You only need to provide the `bestStoryCount` parameter to indicate how many of the story summaries you wish to retrieve.
6. Alternatively you can use a command-line tool, such as cURL, to test the RESTful API - for example the following call retrieves the top 5 stories from the HackerNewsApi: `curl http://localhost:5000/api/v1/news/beststories/5`
7. Also, if you have an IDE (such as Microsoft VisualStudio or IntelliJ Rider), you can simply load the slnx file and execute the application directly.

# Configuration
The `appsettings.json` file allows the service to be configured.  Apart from the default asp.net logging configuration, the following blocks are available to modify the behaviour of the service (shown here with the default values):
```
...
"TargetApi": {
    "BaseUrl": "https://hacker-news.firebaseio.com/v0/",
    "TimeoutSeconds": 15,
    "MaxConcurrentRequests": 3,
    "MaxQueuedRequests": 512
},
"Caching": {
    "BestStoriesCacheExpirationSeconds": 60,
    "StorySummaryCacheExpirationSeconds": 300
}
...
```
The keys work as follows:
* `TargetApi.BaseUrl` - Sets the API URL used to retrieve the data from HackerNews.
* `TargetApi.TimeoutSeconds` - How long each HTTP request to the API will have until they are considered timed out.
* `TargetApi.MaxConcurrentRequests` - How many requests in parallel may be made to the API to avoid overloading it.
* `TargetApi.MaxQueuedRequests` - Once the `TargetApi.MaxConcurrentRequests` limit is reached, this is how many requests should be queued for sending to the API before an exception is thrown.
* `Caching.BestStoriesCacheExpirationSeconds` - Sets how long the list of Best Story Ids should be cached in memory before expiring (the data will be re-fected from the API on the next incoming request).
* `Caching.StorySummaryCacheExpirationSeconds` - Sets how long each individual Story Summary should be cached in memory before expiring.

# Assumptions

1. HackerNews API does not require any authentication, nor identifying headers.
2. HackerNews API is able to service a number of requests in parallel, default configuration sets this limit to 3 on the assumption that this is a reasonably small amount to avoid overloading their API.
3. HackerNews API `beststories.json` call returns story ids already sorted in descending order based upon the underlying record's `score` value.
4. HackerNews API returns valid data in JSON format.
5. No Authentication is required on incoming calls to the service.
6. No rate limiting is required on incoming calls to the service.
7. The service does not require HTTPS to be configured for this example execution.

# Improvements

1. Rate limiting could be added to limit incoming calls to the service.  This could be achieved by using the `.AddRateLimiter()` asp.net middleware.  Correct values for rate limiting vary based upon the hardware/VM infrastructure that this service is running on.
2. Retry logic could be added to handle HTTP request errors if they are thrown by the call to the remote API.
3. A distributed/persisted cache could be plugged in to the existing `HybridCache` instance to allow multiple nodes of the service to be run behind a load-balancer.  This would improve throughput for our consumers whilst keeping the number of calls to the remote API low (although there would be a small opportunity for repeated calls to be made to the backend if the persisted cache is cold - this would need to be addressed via distributed locking between the nodes if a strict rate limit was required on the remote API).
4. Logging is currently using the default in-built console logging, this would need to be converted to use a more robust logging framework (such as Serilog) which would ship logs to a remote logging store or at very least store as a set of rotating files on disk.  Also some asp.net log-level filtering is probably desirable to reduce the output of the Http-related operations.
5. Telemetry could be added to ship metrics and potentially activity traces to an observability platform (in addition to also shipping the log messages to this platform too).  Typically this would be achieved with an OpenTelemetry exporter.
6. In a real deployment HTTPS should be configured and used for the service.
