namespace InvestIt.Services.Interfaces;

public interface IChainMonitorService
{
    Task MonitorWalletsAsync();
    string NetworkName { get; }
    int NetworkId { get; }
}
