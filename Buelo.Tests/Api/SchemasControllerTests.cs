using Buelo.Api.Controllers;
using Buelo.Engine.Declarative.Schema;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Tests.Api;

public class SchemasControllerTests
{
    [Fact]
    public void List_ReturnsTheDeclarativeKinds()
    {
        var result = new SchemasController().List();

        var ok = Assert.IsType<OkObjectResult>(result);
        var kinds = Assert.IsAssignableFrom<IReadOnlyCollection<string>>(ok.Value);
        Assert.Contains("report", kinds);
        Assert.Contains("validator", kinds);
    }

    [Fact]
    public void Get_KnownKind_ReturnsItsSchema()
    {
        var result = new SchemasController().Get("report");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public void Get_UnknownKind_ReturnsNotFound()
    {
        var result = new SchemasController().Get("does-not-exist");

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
