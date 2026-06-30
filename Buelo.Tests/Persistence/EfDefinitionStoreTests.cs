using Buelo.Persistence;

namespace Buelo.Tests.Persistence;

public class EfDefinitionStoreTests
{
    [Fact]
    public async Task Save_read_list_and_delete_roundtrip()
    {
        using var factory = new TestDbFactory();
        var store = new EfDefinitionStore(factory);

        Assert.Null(await store.ReadAsync("report", "invoice"));

        await store.SaveAsync("report", "invoice", "kind: report\nname: invoice");
        await store.SaveAsync("report", "hello", "kind: report\nname: hello");
        await store.SaveAsync("styles", "corporate", "kind: styles");

        Assert.Equal("kind: report\nname: invoice", await store.ReadAsync("report", "invoice"));

        var reports = await store.ListAsync("report");
        Assert.Equal(2, reports.Count);
        Assert.Contains("invoice", reports);
        Assert.Contains("hello", reports);
        Assert.Single(await store.ListAsync("styles"));

        Assert.True(await store.DeleteAsync("report", "invoice"));
        Assert.False(await store.DeleteAsync("report", "invoice"));
        Assert.Null(await store.ReadAsync("report", "invoice"));
    }

    [Fact]
    public async Task Save_overwrites_existing()
    {
        using var factory = new TestDbFactory();
        var store = new EfDefinitionStore(factory);

        await store.SaveAsync("lib", "sales", "v1");
        await store.SaveAsync("lib", "sales", "v2");

        Assert.Equal("v2", await store.ReadAsync("lib", "sales"));
        Assert.Single(await store.ListAsync("lib"));
    }
}
