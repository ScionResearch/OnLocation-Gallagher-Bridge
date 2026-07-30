namespace OnLocationGallagherBridge.Services;

public static class RecordGroupDisplay
{
    public static string GetName(string? id) => id?.ToLowerInvariant() switch
    {
        "employees" => "Staff",
        "contractor-members" => "Contractors",
        _ => id ?? string.Empty
    };
}
