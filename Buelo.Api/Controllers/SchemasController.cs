using Buelo.Engine.Declarative.Schema;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Api.Controllers;

/// <summary>Serves JSON Schemas for each declarative kind, for editor autocomplete (blueprint §11).</summary>
[ApiController]
[Route("api/schemas")]
public class SchemasController : ControllerBase
{
    [HttpGet]
    public IActionResult List() => Ok(DeclarativeSchemas.Kinds);

    [HttpGet("{kind}")]
    public IActionResult Get(string kind)
    {
        if (!DeclarativeSchemas.TryGetType(kind, out _))
            return NotFound(new { error = $"No schema for kind '{kind}'." });

        return Ok(DeclarativeSchemas.Generate(kind));
    }
}
