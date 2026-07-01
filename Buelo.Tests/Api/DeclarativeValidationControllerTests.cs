using Buelo.Api.Controllers;
using Buelo.Contracts;
using Buelo.Engine.Declarative;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Tests.Api;

public class DeclarativeValidationControllerTests
{
    private const string CpfValidator = """
        kind: validator
        name: cpf
        format: "###.###.###-##"
        rules:
          - { digits: 11 }
          - { checksum: { scheme: mod11, weights: [10, 9, 8, 7, 6, 5, 4, 3, 2] } }
        """;

    private static DeclarativeValidationController CreateController()
        => new(new DeclarativeValidator(), new ValidatorExtensions());

    [Fact]
    public void Validate_ValidValue_ReturnsOkValid()
    {
        var result = CreateController().Validate(new DataValidationRequest
        {
            Validator = "cpf",
            Value = "529.982.247-25",
            Modules = [CpfValidator],
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var validation = Assert.IsType<DataValidationResult>(ok.Value);
        Assert.True(validation.Valid);
    }

    [Fact]
    public void Validate_InvalidValue_ReturnsOkInvalid()
    {
        var result = CreateController().Validate(new DataValidationRequest
        {
            Validator = "cpf",
            Value = "529.982.247-00", // bad checksum
            Modules = [CpfValidator],
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var validation = Assert.IsType<DataValidationResult>(ok.Value);
        Assert.False(validation.Valid);
    }

    [Fact]
    public void Validate_UnknownValidator_ReturnsBadRequest()
    {
        var result = CreateController().Validate(new DataValidationRequest
        {
            Validator = "not-defined",
            Value = "x",
            Modules = [CpfValidator],
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
