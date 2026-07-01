using Buelo.Api.Controllers;
using Buelo.Contracts;
using Buelo.Engine;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Tests.Api;

public class WorkspaceControllerTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceController _controller;

    public WorkspaceControllerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"buelo-workspace-api-{Guid.NewGuid()}");
        var store = new FileSystemWorkspaceStore(_root);
        _controller = new WorkspaceController(store);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task CreateFolderAndFile_ThenGetTree_ReturnsNodes()
    {
        var createFolder = await _controller.CreateFolder(new CreateFolderRequest("reports"));
        Assert.IsType<NoContentResult>(createFolder);

        var createFile = await _controller.CreateFile(new CreateFileRequest("reports/main.buelo", "report title:\n  text: Hi"));
        Assert.IsType<OkObjectResult>(createFile);

        var treeResult = await _controller.GetTree();
        var ok = Assert.IsType<OkObjectResult>(treeResult);
        var nodes = Assert.IsAssignableFrom<IEnumerable<WorkspaceNode>>(ok.Value);
        Assert.NotEmpty(nodes);
    }

    [Fact]
    public async Task GetFileContent_Existing_ReturnsOk()
    {
        await _controller.CreateFile(new CreateFileRequest("a.json", "{\"x\":1}"));

        var result = await _controller.GetFileContent("a.json");

        var ok = Assert.IsType<OkObjectResult>(result);
        var file = Assert.IsType<WorkspaceFileRecord>(ok.Value);
        Assert.Contains("\"x\":1", file.Content);
    }

    [Fact]
    public async Task GetFileContent_Missing_ReturnsNotFound()
    {
        var result = await _controller.GetFileContent("nope.json");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CreateFile_DuplicateWithoutOverwrite_ReturnsBadRequest()
    {
        await _controller.CreateFile(new CreateFileRequest("dup.json", "{}"));

        var again = await _controller.CreateFile(new CreateFileRequest("dup.json", "{}"));

        Assert.IsType<BadRequestObjectResult>(again);
    }

    [Fact]
    public async Task SaveFileContent_Existing_UpdatesContent()
    {
        await _controller.CreateFile(new CreateFileRequest("edit.json", "{\"v\":1}"));

        var result = await _controller.SaveFileContent(new SaveFileRequest("edit.json", "{\"v\":2}"));

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("\"v\":2", Assert.IsType<WorkspaceFileRecord>(ok.Value).Content);
    }

    [Fact]
    public async Task SaveFileContent_Missing_WithCreateIfMissing_Creates()
    {
        var result = await _controller.SaveFileContent(new SaveFileRequest("new.json", "{}", CreateIfMissing: true));
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SaveFileContent_Missing_WithoutCreateIfMissing_ReturnsNotFound()
    {
        var result = await _controller.SaveFileContent(new SaveFileRequest("missing.json", "{}"));
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task MoveFile_MovesToNewPath()
    {
        await _controller.CreateFile(new CreateFileRequest("src.json", "{}"));

        var moved = await _controller.MoveFile(new MoveNodeRequest("src.json", "sub/dst.json"));
        Assert.IsType<NoContentResult>(moved);

        Assert.IsType<NotFoundObjectResult>(await _controller.GetFileContent("src.json"));
        Assert.IsType<OkObjectResult>(await _controller.GetFileContent("sub/dst.json"));
    }

    [Fact]
    public async Task MoveFile_Missing_ReturnsNotFound()
    {
        var result = await _controller.MoveFile(new MoveNodeRequest("ghost.json", "dst.json"));
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RenameFile_ChangesName()
    {
        await _controller.CreateFile(new CreateFileRequest("old.json", "{}"));

        var renamed = await _controller.RenameFile(new RenameNodeRequest("old.json", "renamed.json"));
        Assert.IsType<NoContentResult>(renamed);

        Assert.IsType<OkObjectResult>(await _controller.GetFileContent("renamed.json"));
    }

    [Fact]
    public async Task RenameFile_Missing_ReturnsNotFound()
    {
        var result = await _controller.RenameFile(new RenameNodeRequest("ghost.json", "x.json"));
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task DeleteNode_RemovesFile()
    {
        await _controller.CreateFolder(new CreateFolderRequest("data"));
        await _controller.CreateFile(new CreateFileRequest("data/mock.json", "{}"));

        var deleted = await _controller.DeleteNode("data/mock.json");
        Assert.IsType<NoContentResult>(deleted);

        var missing = await _controller.GetFileContent("data/mock.json");
        Assert.IsType<NotFoundObjectResult>(missing);
    }

    [Fact]
    public async Task DeleteNode_Missing_ReturnsNotFound()
    {
        var result = await _controller.DeleteNode("nope.json");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetTypeDeclarations_ValidJson_ReturnsDeclarations()
    {
        await _controller.CreateFile(new CreateFileRequest("model.json", "{\"name\":\"Alice\",\"age\":30}"));

        var result = await _controller.GetTypeDeclarations("model.json");

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetTypeDeclarations_EmptyPath_ReturnsBadRequest()
    {
        var result = await _controller.GetTypeDeclarations("");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetTypeDeclarations_NonJson_ReturnsBadRequest()
    {
        var result = await _controller.GetTypeDeclarations("report.cs");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetTypeDeclarations_Missing_ReturnsNotFound()
    {
        var result = await _controller.GetTypeDeclarations("ghost.json");
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetTypeDeclarations_InvalidJson_ReturnsBadRequest()
    {
        await _controller.CreateFile(new CreateFileRequest("bad.json", "{ not valid json "));

        var result = await _controller.GetTypeDeclarations("bad.json");

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
