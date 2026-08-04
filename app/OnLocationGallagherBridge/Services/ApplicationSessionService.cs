namespace OnLocationGallagherBridge.Services;

public interface IApplicationSessionService
{
    string SessionToken { get; }
}

public class ApplicationSessionService : IApplicationSessionService
{
    public string SessionToken { get; } = Guid.NewGuid().ToString("N");
}
