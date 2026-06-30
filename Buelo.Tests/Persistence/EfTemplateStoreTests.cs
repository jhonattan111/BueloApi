using Buelo.Contracts;
using Buelo.Persistence;

namespace Buelo.Tests.Persistence;

public class EfTemplateStoreTests
{
    private static TemplateRecord NewTemplate(string code) => new()
    {
        Name = "demo",
        Template = code,
        MockData = new { value = 1 },
        Artefacts = [new TemplateArtefact { Name = "mock", Extension = ".json", Content = "{}" }]
    };

    [Fact]
    public async Task Save_assigns_id_and_roundtrips_record()
    {
        using var factory = new TestDbFactory();
        var store = new EfTemplateStore(factory);

        var saved = await store.SaveAsync(NewTemplate("class A {}"));
        Assert.NotEqual(Guid.Empty, saved.Id);

        var fetched = await store.GetAsync(saved.Id);
        Assert.NotNull(fetched);
        Assert.Equal("class A {}", fetched!.Template);
        Assert.Equal("demo", fetched.Name);
        Assert.Single(fetched.Artefacts);
        Assert.Equal("mock", fetched.Artefacts[0].Name);

        Assert.Single(await store.ListAsync());
    }

    [Fact]
    public async Task Resave_snapshots_previous_version()
    {
        using var factory = new TestDbFactory();
        var store = new EfTemplateStore(factory);

        var saved = await store.SaveAsync(NewTemplate("class V1 {}"));
        saved.Template = "class V2 {}";
        await store.SaveAsync(saved);

        Assert.Equal("class V2 {}", (await store.GetAsync(saved.Id))!.Template);

        var versions = await store.GetVersionsAsync(saved.Id);
        Assert.Single(versions);
        Assert.Equal(1, versions[0].Version);
        Assert.Equal("class V1 {}", versions[0].Template);

        var v1 = await store.GetVersionAsync(saved.Id, 1);
        Assert.NotNull(v1);
        Assert.Equal("class V1 {}", v1!.Template);
    }

    [Fact]
    public async Task Delete_removes_template_and_versions()
    {
        using var factory = new TestDbFactory();
        var store = new EfTemplateStore(factory);

        var saved = await store.SaveAsync(NewTemplate("class A {}"));
        saved.Template = "class B {}";
        await store.SaveAsync(saved);

        Assert.True(await store.DeleteAsync(saved.Id));
        Assert.Null(await store.GetAsync(saved.Id));
        Assert.Empty(await store.GetVersionsAsync(saved.Id));
        Assert.False(await store.DeleteAsync(saved.Id));
    }
}
