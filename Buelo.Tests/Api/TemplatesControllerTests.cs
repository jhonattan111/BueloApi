using Buelo.Api.Controllers;
using Buelo.Contracts;
using Buelo.Engine;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Tests.Api;

public class TemplatesControllerTests
{
    private static (TemplatesController controller, InMemoryTemplateStore store) Create()
    {
        var store = new InMemoryTemplateStore();
        return (new TemplatesController(store), store);
    }

    private static Task<TemplateRecord> SeedAsync(InMemoryTemplateStore store, bool withArtefact = false)
        => store.SaveAsync(new TemplateRecord
        {
            Name = "Invoice",
            Template = "// C# report template",
            Mode = TemplateMode.FullClass,
            MockData = new { name = "Alice" },
            Artefacts = withArtefact
                ? [new TemplateArtefact { Path = "helpers/tax.helpers.cs", Name = "tax", Extension = ".helpers.cs", Content = "// helper" }]
                : [],
        });

    // ── CRUD ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsSavedTemplates()
    {
        var (controller, store) = Create();
        await SeedAsync(store);

        var ok = Assert.IsType<OkObjectResult>(await controller.List());
        Assert.NotEmpty(Assert.IsAssignableFrom<IEnumerable<TemplateRecord>>(ok.Value));
    }

    [Fact]
    public async Task Create_ShouldAssignIdAndReturnCreatedAtAction()
    {
        var (controller, _) = Create();

        var result = await controller.Create(new TemplateRecord { Name = "Invoice", Mode = TemplateMode.FullClass, Template = "// C#" });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var saved = Assert.IsType<TemplateRecord>(created.Value);
        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.Equal(nameof(TemplatesController.Get), created.ActionName);
    }

    [Fact]
    public async Task Get_Existing_ReturnsOk()
    {
        var (controller, store) = Create();
        var seeded = await SeedAsync(store);

        var ok = Assert.IsType<OkObjectResult>(await controller.Get(seeded.Id));
        Assert.Equal(seeded.Id, Assert.IsType<TemplateRecord>(ok.Value).Id);
    }

    [Fact]
    public async Task Get_Missing_ReturnsNotFound()
    {
        var (controller, _) = Create();
        Assert.IsType<NotFoundObjectResult>(await controller.Get(Guid.NewGuid()));
    }

    [Fact]
    public async Task Update_Existing_ReturnsOkAndKeepsCreatedAt()
    {
        var (controller, store) = Create();
        var seeded = await SeedAsync(store);

        var edit = new TemplateRecord { Name = "Invoice v2", Template = "// v2", Mode = TemplateMode.FullClass };
        var ok = Assert.IsType<OkObjectResult>(await controller.Update(seeded.Id, edit));
        var saved = Assert.IsType<TemplateRecord>(ok.Value);
        Assert.Equal(seeded.Id, saved.Id);
        Assert.Equal(seeded.CreatedAt, saved.CreatedAt);
    }

    [Fact]
    public async Task Update_Missing_ReturnsNotFound()
    {
        var (controller, _) = Create();
        Assert.IsType<NotFoundObjectResult>(await controller.Update(Guid.NewGuid(), new TemplateRecord { Name = "x" }));
    }

    [Fact]
    public async Task Delete_Existing_ReturnsNoContent()
    {
        var (controller, store) = Create();
        var seeded = await SeedAsync(store);

        Assert.IsType<NoContentResult>(await controller.Delete(seeded.Id));
        Assert.Null(await store.GetAsync(seeded.Id));
    }

    [Fact]
    public async Task Delete_Missing_ReturnsNotFound()
    {
        var (controller, _) = Create();
        Assert.IsType<NotFoundObjectResult>(await controller.Delete(Guid.NewGuid()));
    }

    // ── Artefacts ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListArtefacts_ReturnsArtefacts_OrNotFound()
    {
        var (controller, store) = Create();
        var seeded = await SeedAsync(store, withArtefact: true);

        Assert.IsType<OkObjectResult>(await controller.ListArtefacts(seeded.Id));
        Assert.IsType<NotFoundObjectResult>(await controller.ListArtefacts(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetArtefact_FoundAndNotFound()
    {
        var (controller, store) = Create();
        var seeded = await SeedAsync(store, withArtefact: true);

        Assert.IsType<OkObjectResult>(await controller.GetArtefact(seeded.Id, "tax"));
        Assert.IsType<NotFoundObjectResult>(await controller.GetArtefact(seeded.Id, "ghost"));
        Assert.IsType<NotFoundObjectResult>(await controller.GetArtefact(Guid.NewGuid(), "tax"));
    }

    [Fact]
    public async Task UpsertArtefact_CreatesThenUpdates()
    {
        var (controller, store) = Create();
        var seeded = await SeedAsync(store);

        var created = await controller.UpsertArtefact(seeded.Id, "notes", new UpsertArtefactRequest(".txt", "hello"));
        Assert.IsType<OkObjectResult>(created);

        var updated = await controller.UpsertArtefact(seeded.Id, "notes", new UpsertArtefactRequest(".txt", "hello again"));
        Assert.IsType<OkObjectResult>(updated);

        Assert.IsType<NotFoundObjectResult>(await controller.UpsertArtefact(Guid.NewGuid(), "x", new UpsertArtefactRequest(".txt", "y")));
    }

    [Fact]
    public async Task DeleteArtefact_FoundAndNotFound()
    {
        var (controller, store) = Create();
        var seeded = await SeedAsync(store, withArtefact: true);

        Assert.IsType<NoContentResult>(await controller.DeleteArtefact(seeded.Id, "tax"));
        Assert.IsType<NotFoundObjectResult>(await controller.DeleteArtefact(seeded.Id, "tax")); // already gone
        Assert.IsType<NotFoundObjectResult>(await controller.DeleteArtefact(Guid.NewGuid(), "tax"));
    }

    // ── File-oriented API ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListFiles_ReturnsCoreFilesAndArtefacts_OrNotFound()
    {
        var (controller, store) = Create();
        var seeded = await SeedAsync(store, withArtefact: true);

        var ok = Assert.IsType<OkObjectResult>(await controller.ListFiles(seeded.Id));
        Assert.True(Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value).Count() >= 3);
        Assert.IsType<NotFoundObjectResult>(await controller.ListFiles(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpsertFile_TemplateCore_UpdatesTemplateAndMode()
    {
        var (controller, store) = Create();
        var seeded = await SeedAsync(store);

        Assert.IsType<OkObjectResult>(await controller.UpsertFile(seeded.Id,
            new UpsertTemplateFileRequest("template.report.cs", "// Updated", "template", "FullClass")));

        var reloaded = await store.GetAsync(seeded.Id);
        Assert.Equal("// Updated", reloaded!.Template);
    }

    [Fact]
    public async Task UpsertFile_MockData_ValidAndInvalidJson()
    {
        var (controller, store) = Create();
        var seeded = await SeedAsync(store);

        Assert.IsType<OkObjectResult>(await controller.UpsertFile(seeded.Id,
            new UpsertTemplateFileRequest("data/mock.data.json", "{\"name\":\"Bob\"}")));

        Assert.IsType<BadRequestObjectResult>(await controller.UpsertFile(seeded.Id,
            new UpsertTemplateFileRequest("data/mock.data.json", "{ not json")));
    }

    [Fact]
    public async Task UpsertFile_Artefact_AddsFile_OrNotFoundTemplate()
    {
        var (controller, store) = Create();
        var seeded = await SeedAsync(store);

        Assert.IsType<OkObjectResult>(await controller.UpsertFile(seeded.Id,
            new UpsertTemplateFileRequest("partials/header.cs", "// partial")));

        Assert.IsType<NotFoundObjectResult>(await controller.UpsertFile(Guid.NewGuid(),
            new UpsertTemplateFileRequest("x.cs", "y")));
    }

    [Fact]
    public async Task DeleteFile_CoreForbidden_MissingNotFound_ArtefactSuccess()
    {
        var (controller, store) = Create();
        var seeded = await SeedAsync(store, withArtefact: true);

        Assert.IsType<BadRequestObjectResult>(await controller.DeleteFile(seeded.Id, "template.report.cs"));
        Assert.IsType<NotFoundObjectResult>(await controller.DeleteFile(seeded.Id, "ghost.cs"));
        Assert.IsType<NoContentResult>(await controller.DeleteFile(seeded.Id, "helpers/tax.helpers.cs"));
    }

    // ── Versions ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Versions_List_Get_Restore()
    {
        var (controller, store) = Create();
        var seeded = await SeedAsync(store);
        // A second save of the same id produces a version snapshot.
        seeded.Template = "// edited";
        await store.SaveAsync(seeded);

        var listOk = Assert.IsType<OkObjectResult>(await controller.ListVersions(seeded.Id));
        Assert.NotEmpty(Assert.IsAssignableFrom<IEnumerable<object>>(listOk.Value));

        Assert.IsType<OkObjectResult>(await controller.GetVersion(seeded.Id, 1));
        Assert.IsType<NotFoundObjectResult>(await controller.GetVersion(seeded.Id, 999));

        Assert.IsType<OkObjectResult>(await controller.RestoreVersion(seeded.Id, 1));
        Assert.IsType<NotFoundObjectResult>(await controller.RestoreVersion(seeded.Id, 999));
    }

    [Fact]
    public async Task Versions_MissingTemplate_ReturnsNotFound()
    {
        var (controller, _) = Create();
        var id = Guid.NewGuid();

        Assert.IsType<NotFoundObjectResult>(await controller.ListVersions(id));
        Assert.IsType<NotFoundObjectResult>(await controller.GetVersion(id, 1));
        Assert.IsType<NotFoundObjectResult>(await controller.RestoreVersion(id, 1));
    }
}
