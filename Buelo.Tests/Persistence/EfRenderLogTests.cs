using Buelo.Contracts;
using Buelo.Persistence;

namespace Buelo.Tests.Persistence;

public class EfRenderLogTests
{
    [Fact]
    public async Task Logs_and_reads_back_recent_newest_first()
    {
        using var factory = new TestDbFactory();
        var log = new EfRenderLog(factory);

        await log.LogAsync(new RenderEvent { ReportName = "invoice", ByteCount = 123, Success = true, CreatedAt = DateTimeOffset.UtcNow });
        await log.LogAsync(new RenderEvent { ReportName = "payroll", Success = false, Error = "boom", CreatedAt = DateTimeOffset.UtcNow });

        var recent = await log.RecentAsync(10);

        Assert.Equal(2, recent.Count);
        Assert.Equal("payroll", recent[0].ReportName); // newest first
        Assert.False(recent[0].Success);
        Assert.Equal("boom", recent[0].Error);
        Assert.True(recent[1].Success);
    }
}
