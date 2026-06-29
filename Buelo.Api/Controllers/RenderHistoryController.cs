using Buelo.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Buelo.Api.Controllers;

/// <summary>Operational render history (backed by the EF store when persistence is configured).</summary>
[ApiController]
[Route("api/render-history")]
public class RenderHistoryController(IRenderLog renderLog) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Recent([FromQuery] int count = 50)
        => Ok(await renderLog.RecentAsync(count));
}
