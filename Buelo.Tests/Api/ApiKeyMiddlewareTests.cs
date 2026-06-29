using Buelo.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Buelo.Tests.Api;

public class ApiKeyMiddlewareTests
{
    private sealed class FakeEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Buelo.Api";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(string? apiKey)
    {
        var values = new Dictionary<string, string?>();
        if (apiKey is not null)
            values["Buelo:ApiKey"] = apiKey;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static async Task<(int Status, bool NextCalled)> Run(string? configuredKey, string? authHeader, string path)
    {
        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new ApiKeyMiddleware(next, Config(configuredKey), new FakeEnv());

        var context = new DefaultHttpContext { RequestServices = new ServiceCollection().AddOptions().AddLogging().BuildServiceProvider() };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (authHeader is not null)
            context.Request.Headers.Authorization = authHeader;

        await middleware.Invoke(context);
        return (context.Response.StatusCode, nextCalled);
    }

    [Fact]
    public async Task No_key_configured_passes_through()
    {
        var (_, nextCalled) = await Run(configuredKey: null, authHeader: null, "/api/report/render");
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Correct_bearer_passes()
    {
        var (_, nextCalled) = await Run("secret", "Bearer secret", "/api/report/render");
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Missing_key_returns_401()
    {
        var (status, nextCalled) = await Run("secret", authHeader: null, "/api/report/render");
        Assert.Equal(401, status);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Wrong_key_returns_401()
    {
        var (status, _) = await Run("secret", "Bearer nope", "/api/report/render");
        Assert.Equal(401, status);
    }

    [Fact]
    public async Task Ping_is_public_even_with_key()
    {
        var (_, nextCalled) = await Run("secret", authHeader: null, "/ping");
        Assert.True(nextCalled);
    }
}
