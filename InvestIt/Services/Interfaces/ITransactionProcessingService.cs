using InvestIt.Data.Entities;
using InvestIt.Services.ExplorerClients.Models;

namespace InvestIt.Services.Interfaces;

public interface ITransactionProcessingService
{
    Transaction ProcessTransaction(BlockchainTransaction blockchainTx, int walletId, string monitoredWalletAddress);
    Task<Transaction?> ProcessAndStoreAsync(BlockchainTransaction blockchainTx, int walletId, string monitoredWalletAddress);
}
