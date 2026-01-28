-- Seed some high-profile crypto wallets for monitoring

-- Ethereum Wallets
INSERT INTO MonitoredWallets (Address, BlockchainNetworkId, Label, IsActive, CreatedAt, LastCheckedAt)
VALUES
    -- Vitalik Buterin's public wallet
    ('0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045', 1, 'Vitalik Buterin', 1, GETUTCDATE(), NULL),

    -- Binance Hot Wallet (very active)
    ('0x28C6c06298d514Db089934071355E5743bf21d60', 1, 'Binance Hot Wallet', 1, GETUTCDATE(), NULL),

    -- Uniswap V2 Router (extremely high volume)
    ('0x7a250d5630B4cF539739dF2C5dAcb4c659F2488D', 1, 'Uniswap V2 Router', 1, GETUTCDATE(), NULL),

    -- Top Ethereum Whale Wallet
    ('0x00000000219ab540356cBB839Cbe05303d7705Fa', 1, 'Ethereum 2.0 Deposit Contract', 1, GETUTCDATE(), NULL);

-- BSC Wallets
INSERT INTO MonitoredWallets (Address, BlockchainNetworkId, Label, IsActive, CreatedAt, LastCheckedAt)
VALUES
    -- PancakeSwap Router (very active on BSC)
    ('0x10ED43C718714eb63d5aA57B78B54704E256024E', 2, 'PancakeSwap Router', 1, GETUTCDATE(), NULL),

    -- Binance BSC Hot Wallet
    ('0x8894E0a0c962CB723c1976a4421c95949bE2D4E3', 2, 'Binance BSC Hot Wallet', 1, GETUTCDATE(), NULL);

-- Polygon Wallets
INSERT INTO MonitoredWallets (Address, BlockchainNetworkId, Label, IsActive, CreatedAt, LastCheckedAt)
VALUES
    -- QuickSwap Router (active on Polygon)
    ('0xa5E0829CaCEd8fFDD4De3c43696c57F7D7A678ff', 3, 'QuickSwap Router', 1, GETUTCDATE(), NULL),

    -- Polygon Bridge
    ('0x40ec5B33f54e0E8A33A975908C5BA1c14e5BbbDf', 3, 'Polygon Bridge', 1, GETUTCDATE(), NULL);
