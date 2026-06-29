using Buelo.Contracts;
using Buelo.Engine.Declarative;
using Buelo.Engine.Declarative.Modules;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Api.Controllers;

/// <summary>Validates values against declarative validators (blueprint §8).</summary>
[ApiController]
[Route("api/validate-data")]
public class DeclarativeValidationController(DeclarativeValidator validator, IValidatorExtensions extensions) : ControllerBase
{
    [HttpPost]
    public IActionResult Validate([FromBody] DataValidationRequest request)
    {
        ModuleRegistry registry;
        try
        {
            registry = ModuleRegistry.Build(request.Modules ?? []);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        if (!registry.TryGetValidator(request.Validator, out var module))
            return BadRequest(new { error = $"Validator '{request.Validator}' was not found." });

        var result = validator.Validate(module, request.Value, registry.CreateExpressionContext(), extensions);
        return Ok(result);
    }
}
