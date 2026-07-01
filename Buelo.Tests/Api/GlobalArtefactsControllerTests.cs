using Buelo.Api.Controllers;
using Buelo.Contracts;
using Buelo.Engine;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Tests.Api;

public class GlobalArtefactsControllerTests
{
    private static GlobalArtefactsController CreateController(out InMemoryGlobalArtefactStore store)
    {
        store = new InMemoryGlobalArtefactStore();
        return new GlobalArtefactsController(store);
    }

    private static GlobalArtefact Make(string name, string ext, string content = "{}")
        => new() { Id = Guid.Empty, Name = name, Extension = ext, Content = content, Tags = ["t"] };

    private static async Task<GlobalArtefact> SeedAsync(InMemoryGlobalArtefactStore store, string name, string ext)
        => await store.SaveAsync(Make(name, ext));

    [Fact]
    public async Task List_Empty_ReturnsOkEmpty()
    {
        var controller = CreateController(out _);

        var result = await controller.List();

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<GlobalArtefact>>(ok.Value);
        Assert.Empty(items);
    }

    [Fact]
    public async Task List_WithExtensionFilter_ReturnsOnlyMatching()
    {
        var controller = CreateController(out var store);
        await SeedAsync(store, "a", ".json");
        await SeedAsync(store, "b", ".csx");
        await SeedAsync(store, "c", ".json");

        var result = await controller.List(extension: ".json");

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<GlobalArtefact>>(ok.Value);
        Assert.Equal(2, items.Count);
        Assert.All(items, a => Assert.Equal(".json", a.Extension));
    }

    [Fact]
    public async Task Create_AssignsNewIdAndReturnsCreatedAtAction()
    {
        var controller = CreateController(out _);

        var result = await controller.Create(Make("data", ".json"));

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var saved = Assert.IsType<GlobalArtefact>(created.Value);
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal(nameof(GlobalArtefactsController.Get), created.ActionName);
    }

    [Fact]
    public async Task Get_Existing_ReturnsOk()
    {
        var controller = CreateController(out var store);
        var seeded = await SeedAsync(store, "data", ".json");

        var result = await controller.Get(seeded.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(seeded.Id, Assert.IsType<GlobalArtefact>(ok.Value).Id);
    }

    [Fact]
    public async Task Get_Missing_ReturnsNotFound()
    {
        var controller = CreateController(out _);

        var result = await controller.Get(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetByName_MissingExtension_ReturnsBadRequest()
    {
        var controller = CreateController(out _);

        var result = await controller.GetByName("data", extension: null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetByName_Found_ReturnsOk_CaseInsensitive()
    {
        var controller = CreateController(out var store);
        await SeedAsync(store, "Employee", ".JSON");

        var result = await controller.GetByName("employee", extension: ".json");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Employee", Assert.IsType<GlobalArtefact>(ok.Value).Name);
    }

    [Fact]
    public async Task GetByName_Missing_ReturnsNotFound()
    {
        var controller = CreateController(out _);

        var result = await controller.GetByName("nope", extension: ".json");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Update_Existing_ReturnsOkAndKeepsCreatedAt()
    {
        var controller = CreateController(out var store);
        var seeded = await SeedAsync(store, "data", ".json");

        var edit = Make("data", ".json", content: "{\"changed\":true}");
        var result = await controller.Update(seeded.Id, edit);

        var ok = Assert.IsType<OkObjectResult>(result);
        var saved = Assert.IsType<GlobalArtefact>(ok.Value);
        Assert.Equal(seeded.Id, saved.Id);
        Assert.Equal(seeded.CreatedAt, saved.CreatedAt);
        Assert.Contains("changed", saved.Content);
    }

    [Fact]
    public async Task Update_Missing_ReturnsNotFound()
    {
        var controller = CreateController(out _);

        var result = await controller.Update(Guid.NewGuid(), Make("data", ".json"));

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Delete_Existing_ReturnsNoContent()
    {
        var controller = CreateController(out var store);
        var seeded = await SeedAsync(store, "data", ".json");

        var result = await controller.Delete(seeded.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await store.GetAsync(seeded.Id));
    }

    [Fact]
    public async Task Delete_Missing_ReturnsNotFound()
    {
        var controller = CreateController(out _);

        var result = await controller.Delete(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
