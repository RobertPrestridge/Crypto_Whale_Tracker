-- Insert sample transactions to showcase the retro UI

-- Get wallet IDs
DECLARE @EthWallet1 INT = (SELECT TOP 1 Id FROM MonitoredWallets WHERE BlockchainNetworkId = 1);
DECLARE @EthWallet2 INT = (SELECT TOP 1 Id FROM MonitoredWallets WHERE BlockchainNetworkId = 1 AND Id != @EthWallet1);
DECLARE @BscWallet INT = (SELECT TOP 1 Id FROM MonitoredWallets WHERE BlockchainNetworkId = 2);
DECLARE @PolyWallet INT = (SELECT TOP 1 Id FROM MonitoredWallets WHERE BlockchainNetworkId = 3);

-- Sample Ethereum transactions
INSERT INTO Transactions (WalletId, TxHash, FromAddress, ToAddress, TokenSymbol, TokenAddress, Amount, [Type], Timestamp, BlockNumber, GasUsed, GasPrice, CreatedAt)
VALUES
    (@EthWallet1, '0x1a2b3c4d5e6f7g8h9i0j1k2l3m4n5o6p7q8r9s0t1u2v3w4x5y6z', '0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb', (SELECT Address FROM MonitoredWallets WHERE Id = @EthWallet1), 'USDT', '0xdac17f958d2ee523a2206206994597c13d831ec7', 1500.5000, 0, DATEADD(MINUTE, -5, GETUTCDATE()), 18950123, 21000, 0.000025, GETUTCDATE()),
    (@EthWallet1, '0x9f8e7d6c5b4a3928170615243f5e6d7c8b9a0f1e2d3c4b5a', '0x8894E0a0c962CB723c1976a4421c95949bE2D4E3', (SELECT Address FROM MonitoredWallets WHERE Id = @EthWallet1), 'ETH', NULL, 2.5500, 0, DATEADD(MINUTE, -12, GETUTCDATE()), 18950100, 21000, 0.000030, GETUTCDATE()),
    (@EthWallet1, '0x7c6b5a4938271605f4e3d2c1b0a9f8e7d6c5b4a3', (SELECT Address FROM MonitoredWallets WHERE Id = @EthWallet1), '0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb', 'WETH', '0xc02aaa39b223fe8d0a0e5c4f27ead9083c756cc2', 1.2500, 1, DATEADD(MINUTE, -25, GETUTCDATE()), 18950050, 45000, 0.000040, GETUTCDATE()),
    (@EthWallet2, '0x5e4d3c2b1a0f9e8d7c6b5a4938271605f4e3d2c1', '0x28C6c06298d514Db089934071355E5743bf21d60', (SELECT Address FROM MonitoredWallets WHERE Id = @EthWallet2), 'USDC', '0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48', 5000.0000, 0, DATEADD(MINUTE, -45, GETUTCDATE()), 18950000, 21000, 0.000035, GETUTCDATE()),
    (@EthWallet2, '0x3d2c1b0a9f8e7d6c5b4a3928170615243f5e6d7c', (SELECT Address FROM MonitoredWallets WHERE Id = @EthWallet2), '0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb', 'DAI', '0x6b175474e89094c44da98b954eedeac495271d0f', 3500.0000, 1, DATEADD(HOUR, -2, GETUTCDATE()), 18949900, 35000, 0.000028, GETUTCDATE());

-- Sample BSC transactions
IF @BscWallet IS NOT NULL
BEGIN
    INSERT INTO Transactions (WalletId, TxHash, FromAddress, ToAddress, TokenSymbol, TokenAddress, Amount, [Type], Timestamp, BlockNumber, GasUsed, GasPrice, CreatedAt)
    VALUES
        (@BscWallet, '0xb1a2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0', '0x8894E0a0c962CB723c1976a4421c95949bE2D4E3', (SELECT Address FROM MonitoredWallets WHERE Id = @BscWallet), 'CAKE', '0x0e09fabb73bd3ade0a17ecc321fd13a19e81ce82', 125.7500, 0, DATEADD(MINUTE, -8, GETUTCDATE()), 34567890, 150000, 0.000005, GETUTCDATE()),
        (@BscWallet, '0xc2d3e4f5g6h7i8j9k0l1m2n3o4p5q6r7s8t9u0v1', (SELECT Address FROM MonitoredWallets WHERE Id = @BscWallet), '0x10ED43C718714eb63d5aA57B78B54704E256024E', 'BNB', NULL, 0.8500, 1, DATEADD(MINUTE, -30, GETUTCDATE()), 34567800, 21000, 0.000003, GETUTCDATE());
END

-- Sample Polygon transactions
IF @PolyWallet IS NOT NULL
BEGIN
    INSERT INTO Transactions (WalletId, TxHash, FromAddress, ToAddress, TokenSymbol, TokenAddress, Amount, [Type], Timestamp, BlockNumber, GasUsed, GasPrice, CreatedAt)
    VALUES
        (@PolyWallet, '0xd3e4f5g6h7i8j9k0l1m2n3o4p5q6r7s8t9u0v1w2', '0x40ec5B33f54e0E8A33A975908C5BA1c14e5BbbDf', (SELECT Address FROM MonitoredWallets WHERE Id = @PolyWallet), 'MATIC', NULL, 500.0000, 0, DATEADD(MINUTE, -15, GETUTCDATE()), 51234567, 21000, 0.00000001, GETUTCDATE()),
        (@PolyWallet, '0xe4f5g6h7i8j9k0l1m2n3o4p5q6r7s8t9u0v1w2x3', (SELECT Address FROM MonitoredWallets WHERE Id = @PolyWallet), '0xa5E0829CaCEd8fFDD4De3c43696c57F7D7A678ff', 'WMATIC', '0x0d500b1d8e8ef31e21c99d1db9a6444d3adf1270', 200.0000, 1, DATEADD(HOUR, -1, GETUTCDATE()), 51234500, 35000, 0.00000002, GETUTCDATE());
END

PRINT 'Sample transactions inserted successfully!';
