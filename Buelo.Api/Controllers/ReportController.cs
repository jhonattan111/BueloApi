using Buelo.Contracts;
using Buelo.Engine;
using Buelo.Engine.Declarative;
using Buelo.Engine.Renderers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Buelo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportController(
    TemplateEngine engine,
    ITemplateStore store,
    OutputRendererRegistry renderers,
    DeclarativeReportEngine declarative,
    IDefinitionStore definitions,
    IRenderLog renderLog,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Runs a render on a background task bounded by <c>Buelo:RenderTimeoutSeconds</c> (default 30;
    /// 0 disables). Best-effort: QuestPDF is synchronous, so a timed-out render is abandoned, not
    /// cancelled — the cap protects the caller's wait time (handoff §12 hardening).
    /// </summary>
    private async Task<byte[]> RenderWithTimeoutAsync(Func<Task<byte[]>> render)
    {
        var seconds = configuration.GetValue("Buelo:RenderTimeoutSeconds", 30);
        return seconds <= 0
            ? await render()
            : await Task.Run(render).WaitAsync(TimeSpan.FromSeconds(seconds));
    }
    /// <summary>
    /// Renders a report from a C# IDocument template supplied in the request body.
    /// Use ?format=pdf (default) or ?format=excel to select output format.
    /// </summary>
    [HttpPost("render")]
    public async Task<IActionResult> Render([FromBody] ReportRequest request, [FromQuery] string format = "pdf")
    {
        var renderer = renderers.TryGetRenderer(format);
        if (renderer is null)
            return BadRequest(new { error = $"Unsupported format '{format}'." });

        if (!renderer.SupportsMode(request.Mode))
            return BadRequest(new { error = $"Format '{format}' does not support mode '{request.Mode}'." });

        var input = new RendererInput
        {
            Source = request.Template,
            Mode = request.Mode,
            RawData = request.Data,
            PageSettings = request.PageSettings ?? PageSettings.Default(),
        };

        try
        {
            var bytes = await renderer.RenderAsync(input);
            var baseName = Path.GetFileNameWithoutExtension(request.FileName);
            return File(bytes, renderer.ContentType, baseName + renderer.FileExtension);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Renders a report from a declarative YAML definition (engine: declarative).
    /// The definition is YAML; the data is JSON. Produces a PDF.
    /// </summary>
    [HttpPost("render-declarative")]
    public async Task<IActionResult> RenderDeclarative([FromBody] DeclarativeReportRequest request, [FromQuery] string format = "pdf")
    {
        if (string.IsNullOrWhiteSpace(request.Definition))
            return BadRequest(new { error = "Report definition is required." });

        var excel = IsExcel(format);
        var baseName = Path.GetFileNameWithoutExtension(request.FileName);
        try
        {
            var bytes = await RenderWithTimeoutAsync(() => Task.FromResult(excel
                ? declarative.RenderExcel(request.Definition, request.Data, request.Modules)
                : declarative.RenderPdf(request.Definition, request.Data, request.Modules)));
            await renderLog.LogAsync(Event(baseName, bytes.Length, success: true, format: excel ? "excel" : "pdf"));
            return File(bytes, ContentType(excel), baseName + Extension(excel));
        }
        catch (TimeoutException)
        {
            await renderLog.LogAsync(Event(baseName, 0, success: false, "Render timed out."));
            return StatusCode(503, new { error = "Render timed out." });
        }
        catch (InvalidOperationException ex)
        {
            await renderLog.LogAsync(Event(baseName, 0, success: false, ex.Message));
            return BadRequest(new { error = ex.Message });
        }
    }

    private static bool IsExcel(string? format) => string.Equals(format, "excel", StringComparison.OrdinalIgnoreCase);
    private static string ContentType(bool excel) => excel ? BueloDocumentExcelRenderer.ContentType : "application/pdf";
    private static string Extension(bool excel) => excel ? ".xlsx" : ".pdf";

    private static RenderEvent Event(string reportName, int byteCount, bool success, string? error = null, string format = "pdf") => new()
    {
        ReportName = reportName,
        Engine = "declarative",
        Format = format,
        ByteCount = byteCount,
        Success = success,
        Error = error,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// Ejects an equivalent C# IDocument from a declarative report (graduation path, blueprint §10).
    /// </summary>
    [HttpPost("eject")]
    public IActionResult Eject([FromBody] DeclarativeReportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Definition))
            return BadRequest(new { error = "Report definition is required." });

        try
        {
            var source = declarative.EjectCSharp(request.Definition, request.Data, request.Modules);
            return Ok(new { source });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Renders a stored declarative report by name, resolving its imported modules from the
    /// definition store. Produces a PDF.
    /// </summary>
    [HttpPost("render-stored/{name}")]
    public async Task<IActionResult> RenderStored(string name, [FromBody] StoredReportRequest? request = null, [FromQuery] string format = "pdf")
    {
        var excel = IsExcel(format);
        try
        {
            var bytes = await RenderWithTimeoutAsync(() => excel
                ? declarative.RenderStoredExcelAsync(name, request?.Data, definitions)
                : declarative.RenderStoredAsync(name, request?.Data, definitions));
            await renderLog.LogAsync(Event(name, bytes.Length, success: true, format: excel ? "excel" : "pdf"));
            var baseName = Path.GetFileNameWithoutExtension(request?.FileName ?? name);
            return File(bytes, ContentType(excel), baseName + Extension(excel));
        }
        catch (TimeoutException)
        {
            await renderLog.LogAsync(Event(name, 0, success: false, "Render timed out."));
            return StatusCode(503, new { error = "Render timed out." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Validates a C# template by compiling it with Roslyn without generating output.
    /// Always returns 200 OK; the valid field signals success or failure.
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ReportValidateRequest request)
    {
        var result = await engine.ValidateAsync(request.Template, request.Mode);
        return Ok(result);
    }

    /// <summary>
    /// Renders a stored template by its GUID.
    /// Supply ?format=pdf|excel to override the template's default output format.
    /// Supply ?version=N to render from a historical snapshot.
    /// </summary>
    [HttpPost("render/{id:guid}")]
    public async Task<IActionResult> RenderById(
        Guid id,
        [FromQuery] int? version = null,
        [FromBody] TemplateRenderRequest? request = null,
        [FromQuery] string? format = null)
    {
        TemplateRecord? template;

        if (version.HasValue)
        {
            var snapshot = await store.GetVersionAsync(id, version.Value);
            if (snapshot is null)
                return NotFound(new { error = $"Version {version.Value} not found for template '{id}'." });

            var current = await store.GetAsync(id);
            if (current is null)
                return NotFound(new { error = $"Template '{id}' not found." });

            template = new TemplateRecord
            {
                Id = current.Id,
                Name = current.Name,
                Mode = current.Mode,
                OutputFormat = current.OutputFormat,
                PageSettings = current.PageSettings,
                DefaultFileName = current.DefaultFileName,
                MockData = current.MockData,
                Template = snapshot.Template,
                Artefacts = snapshot.Artefacts
            };
        }
        else
        {
            template = await store.GetAsync(id);
            if (template is null)
                return NotFound(new { error = $"Template '{id}' not found." });
        }

        var effectiveFormat = !string.IsNullOrWhiteSpace(format)
            ? format!
            : (template.OutputFormat == OutputFormat.Excel ? "excel" : "pdf");

        var renderer = renderers.TryGetRenderer(effectiveFormat);
        if (renderer is null)
            return BadRequest(new { error = $"Unsupported format '{effectiveFormat}'." });

        if (!renderer.SupportsMode(template.Mode))
            return BadRequest(new { error = $"Format '{effectiveFormat}' does not support mode '{template.Mode}'." });

        var data = request?.Data ?? template.MockData;
        if (data is null)
            return BadRequest(new { error = "No data provided and the template has no mock data configured." });

        var fileName = request?.FileName ?? template.DefaultFileName;
        var pageSettings = request?.PageSettings ?? template.PageSettings;

        try
        {
            var input = new RendererInput
            {
                Source = template.Template,
                Mode = template.Mode,
                RawData = data,
                PageSettings = pageSettings ?? PageSettings.Default(),
            };

            var bytes = await renderer.RenderAsync(input);
            return File(bytes, renderer.ContentType, Path.GetFileNameWithoutExtension(fileName) + renderer.FileExtension);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Renders a stored template using its built-in mock data.
    /// </summary>
    [HttpPost("preview/{id:guid}")]
    public async Task<IActionResult> Preview(Guid id)
    {
        var template = await store.GetAsync(id);
        if (template is null)
            return NotFound(new { error = $"Template '{id}' not found." });

        if (template.MockData is null)
            return BadRequest(new { error = "Template has no mock data configured." });

        try
        {
            var pdf = await engine.RenderTemplateAsync(template, template.MockData, template.PageSettings);
            return File(pdf, "application/pdf", template.DefaultFileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Returns the list of supported output formats and their MIME types.
    /// </summary>
    [HttpGet("formats")]
    public IActionResult GetFormats()
    {
        var formats = renderers.SupportedFormats
            .Select(f =>
            {
                var r = renderers.TryGetRenderer(f)!;
                return new { format = r.Format, contentType = r.ContentType, fileExtension = r.FileExtension };
            });
        return Ok(formats);
    }
}
