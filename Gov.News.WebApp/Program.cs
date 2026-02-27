using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Authentication;
using Gov.News.WebApp;
using Gov.News.Website;
using Gov.News.Website.Hubs;
using Gov.News.Website.Middleware;
using Gov.News.Website.Properties;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Polly;
using Polly.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

#if DEBUG
builder.WebHost.UseUrls("http://localhost:53488/");
#endif
builder.WebHost.UseContentRoot(Directory.GetCurrentDirectory());
builder.WebHost.UseIISIntegration();

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

if (!Debugger.IsAttached)
{
    builder.Configuration.AddEnvironmentVariables();
}

builder.Configuration.Bind(Settings.Default);

if (builder.Configuration["GranvilleTestDate"] != null)
{
    try
    {
        AppState.GranvilleTestDate = DateTime.Parse(builder.Configuration["GranvilleTestDate"]);
    }
    catch (SystemException)
    {
        AppState.GranvilleTestDate = DateTime.MinValue;
    }
}

builder.Services.AddMemoryCache();
builder.Services.AddRazorPages().AddRazorRuntimeCompilation();
builder.Services.AddMvc(options =>
    {
        options.EnableEndpointRouting = false;
    })
    .AddMvcOptions(options =>
    {
#if DEBUG
        var cacheProfile = new CacheProfile { Location = ResponseCacheLocation.None, NoStore = true };
        var cacheProfileNoCache = new CacheProfile { Location = ResponseCacheLocation.None, NoStore = true };
#else
        var cacheProfile = new CacheProfile { Duration = 60 };
        var cacheProfileNoCache = new CacheProfile { Location = ResponseCacheLocation.None, NoStore = true };
#endif
        options.CacheProfiles.Add("Default", cacheProfile);
        options.CacheProfiles.Add("Feed", cacheProfile);
        options.CacheProfiles.Add("Embed", cacheProfile);
        options.CacheProfiles.Add("Page", cacheProfile);
        options.CacheProfiles.Add("Archive", cacheProfile);
        options.CacheProfiles.Add("NoCache", cacheProfileNoCache);

        options.Filters.Add(new TypeFilterAttribute(typeof(XFrameOptionsAttribute)));
        options.Filters.Add(new TypeFilterAttribute(typeof(RequirePermanentHttpsAttribute)));
        options.Filters.Add(new ExceptionReportingFilter());
    })
    .AddRazorRuntimeCompilation();

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

var handler = new HttpClientHandler
{
    SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
};

builder.Services.AddSingleton(new Func<IServiceProvider, Gov.News.Api.IClient>((serviceProvider) =>
{
    var client = new Gov.News.Api.Client(handler);
    client.BaseUri = new Uri(builder.Configuration["NewsApi"]);
    return client;
}));

builder.Services.AddSingleton<Repository, Repository>();
    builder.Services.AddSingleton<IHostedService, LiveHub>();
builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
builder.Services.Replace(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(TimestampLogger<>)));

builder.Services
    .AddHealthChecks()
    .AddUrlGroup(new Uri(builder.Configuration["NewsApi"] + "hc"));

IAsyncPolicy<HttpResponseMessage> cachePolicy =
  Policy.CacheAsync<HttpResponseMessage>(
      cacheProvider: new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions())),
      ttl: new TimeSpan(0, 1, 0)
  );

builder.Services.AddHttpClient();
builder.Services.AddHttpClient("uri-group", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["NewsApi"] + "hc");
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    return new HttpClientHandler
    {
        SslProtocols = SslProtocols.Tls12
    };
}).AddPolicyHandler(cachePolicy);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseBrowserLink();
}
else
{
    app.UseExceptionHandler("/error");
}

app.UseHealthChecks("/hc");
app.UseRedirect();

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers[HeaderNames.CacheControl] = "public,max-age=600";
        ctx.Context.Response.Headers[HeaderNames.Pragma] = "no-cache";
        ctx.Context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        ctx.Context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }
});

#if USE_JAVASCRIPT_SIGNALR
if (Settings.Default.SignalREnabled != null && Settings.Default.SignalREnabled == "true")
{
    app.UseSignalR();
}
#endif

app.UseMvc(routes =>
{
    routes.RegisterRoutes();
});

app.Run();
