using Buelo.Api.Controllers;
using Buelo.Contracts;
using Buelo.Engine;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Tests.Api;

public class RenderHistoryControllerTests
{
    private sealed class FakeRenderLog(params RenderEvent[] events) : IRenderLog
    {
        public int LastCount { get; private set; } = -1;

        public Task LogAsync(RenderEvent renderEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<RenderEvent>> RecentAsync(int count = 50, CancellationToken cancellationToken = default)
        {
            LastCount = count;
            return Task.FromResult<IReadOnlyList<RenderEvent>>(events.Take(count).ToList());
        }
    }

    [Fact]
    public async Task Recent_ReturnsEventsFromTheLog_DefaultCount()
    {
        var log = new FakeRenderLog(
            new RenderEvent { ReportName = "invoice", Success = true },
            new RenderEvent { ReportName = "payroll", Success = false, Error = "boom" });

        var result = await new RenderHistoryController(log).Recent();

        var ok = Assert.IsType<OkObjectResult>(result);
        var events = Assert.IsAssignableFrom<IReadOnlyList<RenderEvent>>(ok.Value);
        Assert.Equal(2, events.Count);
        Assert.Equal(50, log.LastCount); // default count forwarded to the store
    }

    [Fact]
    public async Task Recent_ForwardsTheCountQuery()
    {
        var log = new FakeRenderLog();

        await new RenderHistoryController(log).Recent(count: 5);

        Assert.Equal(5, log.LastCount);
    }

    [Fact]
    public async Task Recent_EmptyLog_ReturnsOkWithEmptyList()
    {
        var result = await new RenderHistoryController(new NullRenderLog()).Recent();

        var ok = Assert.IsType<OkObjectResult>(result);
        var events = Assert.IsAssignableFrom<IReadOnlyList<RenderEvent>>(ok.Value);
        Assert.Empty(events);
    }
}
