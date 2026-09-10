using System.Text.Json;
using OnLocationGallagherBridge.Services;

namespace OnLocationGallagherBridge.Tests;

public class JobProcessorCompetencyTests
{
    private const string CompetencyHref = "https://cc.example/api/cardholders/11651/competencies/a369f3f1f21f45dfb94a2ca5e6d102e5";

    private static JsonElement Cardholder(string? expiry, string href = CompetencyHref)
    {
        var expiryJson = expiry is null ? string.Empty : $",\"expiry\":\"{expiry}\"";
        var json = $$"""
        {
          "href": "https://cc.example/api/cardholders/11651",
          "firstName": "Tim",
          "competencies": [
            {
              "href": "{{href}}",
              "competency": { "href": "https://cc.example/api/competencies/1", "name": "Facilities Induction" },
              "status": { "value": "active", "type": "active" }{{expiryJson}}
            }
          ]
        }
        """;
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static Dictionary<string, object?> UpdateEnvelope(string? expiry, string href = CompetencyHref)
    {
        var update = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["href"] = href };
        if (expiry is not null) update["expiry"] = expiry;
        return new Dictionary<string, object?>
        {
            ["update"] = new List<Dictionary<string, object?>> { update }
        };
    }

    [Fact]
    public void SameExpiry_IsUnchanged()
    {
        var result = JobProcessor.CompetenciesUnchanged(Cardholder("2027-09-04T11:59:59Z"), UpdateEnvelope("2027-09-04T11:59:59Z"));
        Assert.True(result);
    }

    [Fact]
    public void SameInstantDifferentFormat_IsUnchanged()
    {
        // Command Centre may echo the expiry with a different offset representation than the bridge sends.
        var result = JobProcessor.CompetenciesUnchanged(Cardholder("2027-09-04T23:59:59+12:00"), UpdateEnvelope("2027-09-04T11:59:59Z"));
        Assert.True(result);
    }

    [Fact]
    public void DifferentExpiry_IsChanged()
    {
        var result = JobProcessor.CompetenciesUnchanged(Cardholder("2026-09-04T11:59:59Z"), UpdateEnvelope("2027-09-04T11:59:59Z"));
        Assert.False(result);
    }

    [Fact]
    public void ExpiryRemovedInCommandCentre_IsChanged()
    {
        var result = JobProcessor.CompetenciesUnchanged(Cardholder(null), UpdateEnvelope("2027-09-04T11:59:59Z"));
        Assert.False(result);
    }

    [Fact]
    public void NoExpiryOnEitherSide_IsUnchanged()
    {
        var result = JobProcessor.CompetenciesUnchanged(Cardholder(null), UpdateEnvelope(null));
        Assert.True(result);
    }

    [Fact]
    public void UpdateTargetsUnknownLink_IsChanged()
    {
        var result = JobProcessor.CompetenciesUnchanged(Cardholder("2027-09-04T11:59:59Z"), UpdateEnvelope("2027-09-04T11:59:59Z", "https://cc.example/api/cardholders/11651/competencies/other"));
        Assert.False(result);
    }

    [Fact]
    public void AddEnvelope_IsChanged()
    {
        var envelope = new Dictionary<string, object?>
        {
            ["add"] = new List<Dictionary<string, object?>>
            {
                new() { ["competency"] = new Dictionary<string, string> { ["href"] = "https://cc.example/api/competencies/2" }, ["expiry"] = "2027-01-01T00:00:00Z" }
            }
        };
        Assert.False(JobProcessor.CompetenciesUnchanged(Cardholder("2027-09-04T11:59:59Z"), envelope));
    }

    [Fact]
    public void CardholderWithoutCompetencies_IsChanged()
    {
        var cardholder = JsonDocument.Parse("""{"href":"https://cc.example/api/cardholders/11651","firstName":"Tim"}""").RootElement.Clone();
        Assert.False(JobProcessor.CompetenciesUnchanged(cardholder, UpdateEnvelope("2027-09-04T11:59:59Z")));
    }
}
