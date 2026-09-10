using OnLocationGallagherBridge.Services;

namespace OnLocationGallagherBridge.Tests;

public class SyncProfileRunnerPayloadTests
{
    // Shape returned when the person is enumerated from the collection endpoint.
    private const string ListShape = """
        {"id":71104,"name":"Tim Hale","email":"tim.hale@example.org","mobile":"64 298385771","status":"active",
         "onsite_status":"offsite","sp_orgs":[{"id":8021,"name":"Agresearch"}],"logs":[],"tokens":[],
         "inductions":{"433":{"id":248532,"completed":"2026-09-04T12:41:51+12:00","sp_member_id":71104,"renew":"2027-09-04","status":"passed"}}}
        """;

    // Shape returned when the same person is fetched by id: identical apart from the populated change history.
    private const string DetailShape = """
        {"id":71104,"name":"Tim Hale","email":"tim.hale@example.org","mobile":"64 298385771","status":"active",
         "onsite_status":"offsite","sp_orgs":[{"id":8021,"name":"Agresearch"}],
         "logs":[{"time":"2024-09-30T08:25:25+13:00","staff_id":1559652,"type":"modify","data":{"changed":{"service_provider_id":{"from":"","to":"11318"}}}}],
         "tokens":[],
         "inductions":{"433":{"id":248532,"completed":"2026-09-04T12:41:51+12:00","sp_member_id":71104,"renew":"2027-09-04","status":"passed"}}}
        """;

    [Fact]
    public void ListAndDetailShapes_DifferingOnlyInLogs_AreEqual()
    {
        Assert.True(SyncProfileRunner.PayloadsEqual(ListShape, DetailShape));
        Assert.True(SyncProfileRunner.PayloadsEqual(DetailShape, ListShape));
    }

    [Fact]
    public void ChangedMappedField_IsStillDetected()
    {
        var changed = DetailShape.Replace("64 298385771", "64 210000000");
        Assert.False(SyncProfileRunner.PayloadsEqual(ListShape, changed));
    }

    [Fact]
    public void ChangedInductionRenewal_IsStillDetected()
    {
        var changed = DetailShape.Replace("\"renew\":\"2027-09-04\"", "\"renew\":\"2028-09-04\"");
        Assert.False(SyncProfileRunner.PayloadsEqual(ListShape, changed));
    }

    [Fact]
    public void NestedLogsKey_IsNotIgnored()
    {
        // Only the top-level OnLocation 'logs' array is ignored; a nested property of the same name still counts.
        const string a = """{"id":1,"inductions":{"433":{"logs":[]}}}""";
        const string b = """{"id":1,"inductions":{"433":{"logs":[{"x":1}]}}}""";
        Assert.False(SyncProfileRunner.PayloadsEqual(a, b));
    }

    [Fact]
    public void NullOrEmpty_AreNotEqualToContent()
    {
        Assert.False(SyncProfileRunner.PayloadsEqual(null, ListShape));
        Assert.False(SyncProfileRunner.PayloadsEqual("", ListShape));
        Assert.True(SyncProfileRunner.PayloadsEqual(null, null));
    }
}
