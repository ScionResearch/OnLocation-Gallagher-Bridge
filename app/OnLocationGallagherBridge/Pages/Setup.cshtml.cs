using Microsoft.AspNetCore.Mvc.RazorPages;
using OnLocationGallagherBridge.Services;

namespace OnLocationGallagherBridge.Pages;

public class SetupModel : PageModel
{
    private readonly IConfigurationStatusService _statusService;

    public SetupModel(IConfigurationStatusService statusService)
    {
        _statusService = statusService;
    }

    public ConfigurationState Status { get; set; } = new(
        ConfigurationStatus.NotConfigured,
        ConfigurationStatus.NotConfigured,
        ConfigurationStatus.NotConfigured,
        ConfigurationStatus.NotConfigured);

    public async Task OnGetAsync(CancellationToken ct)
    {
        Status = await _statusService.GetOverallStateAsync(false, ct);
    }
}
