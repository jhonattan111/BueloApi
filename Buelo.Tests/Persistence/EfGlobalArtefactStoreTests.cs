using Buelo.Contracts;
using Buelo.Persistence;

namespace Buelo.Tests.Persistence;

public class EfGlobalArtefactStoreTests
{
    [Fact]
    public async Task Save_get_byname_and_tags_roundtrip()
    {
        using var factory = new TestDbFactory();
        var store = new EfGlobalArtefactStore(factory);

        var saved = await store.SaveAsync(new GlobalArtefact
        {
            Name = "employee",
            Extension = ".json",
            Content = "{}",
            Description = "shared",
            Tags = ["hr", "data"]
        });

        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.NotEqual(default, saved.CreatedAt);

        var byId = await store.GetAsync(saved.Id);
        Assert.NotNull(byId);
        Assert.Equal(["hr", "data"], byId!.Tags);

        var byName = await store.GetByNameAsync("employee", ".json");
        Assert.NotNull(byName);
        Assert.Equal(saved.Id, byName!.Id);
    }

    [Fact]
    public async Task List_filters_by_extension()
    {
        using var factory = new TestDbFactory();
        var store = new EfGlobalArtefactStore(factory);

        await store.SaveAsync(new GlobalArtefact { Name = "a", Extension = ".json", Content = "{}" });
        await store.SaveAsync(new GlobalArtefact { Name = "b", Extension = ".csx", Content = "//" });

        Assert.Equal(2, (await store.ListAsync()).Count);
        Assert.Single(await store.ListAsync(".json"));
    }

    [Fact]
    public async Task Update_keeps_created_at_and_changes_content()
    {
        using var factory = new TestDbFactory();
        var store = new EfGlobalArtefactStore(factory);

        var saved = await store.SaveAsync(new GlobalArtefact { Name = "a", Extension = ".json", Content = "1" });
        var createdAt = saved.CreatedAt;

        saved.Content = "2";
        var updated = await store.SaveAsync(saved);

        Assert.Equal("2", (await store.GetAsync(saved.Id))!.Content);
        Assert.Equal(createdAt, updated.CreatedAt);
        Assert.Single(await store.ListAsync());
    }

    [Fact]
    public async Task Delete_removes_artefact()
    {
        using var factory = new TestDbFactory();
        var store = new EfGlobalArtefactStore(factory);

        var saved = await store.SaveAsync(new GlobalArtefact { Name = "a", Extension = ".json", Content = "{}" });
        Assert.True(await store.DeleteAsync(saved.Id));
        Assert.Null(await store.GetAsync(saved.Id));
        Assert.False(await store.DeleteAsync(saved.Id));
    }
}
