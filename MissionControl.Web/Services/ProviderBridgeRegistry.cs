using MissionControl.Models;

namespace MissionControl.Services;

public interface IProviderBridgeRegistry
{
    IProviderBridge? GetBridge(Provider provider);
    void RegisterBridge(ProviderType type, IProviderBridge bridge);
    bool IsProviderAvailable(Provider provider);
}

public class ProviderBridgeRegistry : IProviderBridgeRegistry
{
    private readonly ILogger<ProviderBridgeRegistry> _log;
    private readonly Dictionary<ProviderType, IProviderBridge> _bridges = new();

    public ProviderBridgeRegistry(ILogger<ProviderBridgeRegistry> log)
    {
        _log = log;
    }

    public void RegisterBridge(ProviderType type, IProviderBridge bridge)
    {
        _bridges[type] = bridge;
    }

    public IProviderBridge? GetBridge(Provider provider)
    {
        return _bridges.TryGetValue(provider.Type, out var bridge) ? bridge : null;
    }

    public bool IsProviderAvailable(Provider provider)
    {
        try
        {
            var bridge = GetBridge(provider);
            return bridge?.PingAsync().GetAwaiter().GetResult() ?? false;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Provider {Provider} is not available", provider.Name);
            return false;
        }
    }
}