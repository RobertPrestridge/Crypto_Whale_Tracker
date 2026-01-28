-- Add HIGHLY ACTIVE wallets for real-time transaction monitoring
-- These addresses see thousands of transactions daily

-- Clear existing wallets first (optional - comment out if you want to keep current ones)
-- DELETE FROM Transactions;
-- DELETE FROM MonitoredWallets;

-- ETHEREUM - Super Active Addresses
INSERT INTO MonitoredWallets (Address, Label, BlockchainNetworkId, IsActive, CreatedAt, LastCheckedAt)
VALUES
    -- Uniswap V3 Router - EXTREMELY active
    ('0xE592427A0AEce92De3Edee1F18E0157C05861564', 'Uniswap V3 Router', 1, 1, GETUTCDATE(), NULL),

    -- OpenSea Seaport - High NFT volume
    ('0x00000000000000ADc04C56Bf30aC9d3c0aAF14dC', 'OpenSea Seaport', 1, 1, GETUTCDATE(), NULL),

    -- Tether Treasury - USDT minting
    ('0x5754284f345afc66a98fbB0a0Afe71e0F007B949', 'Tether Treasury', 1, 1, GETUTCDATE(), NULL),

    -- MetaMask Swap Router - Very active
    ('0x881D40237659C251811CEC9c364ef91dC08D300C', 'MetaMask Swap Router', 1, 1, GETUTCDATE(), NULL),

    -- USDC Circle - High volume
    ('0x55FE002aefF02F77364de339a1292923A15844B8', 'USDC Circle', 1, 1, GETUTCDATE(), NULL);

-- BSC - Super Active Addresses
INSERT INTO MonitoredWallets (Address, Label, BlockchainNetworkId, IsActive, CreatedAt, LastCheckedAt)
VALUES
    -- PancakeSwap V3 Router - Most active on BSC
    ('0x13f4EA83D0bd40E75C8222255bc855a974568Dd4', 'PancakeSwap V3 Router', 2, 1, GETUTCDATE(), NULL),

    -- BSC-USD Bridge - High volume
    ('0x47ac0Fb4F2D84898e4D9E7b4DaB3C24507a6D503', 'BSC-USD Bridge', 2, 1, GETUTCDATE(), NULL),

    -- Venus Protocol - DeFi activity
    ('0xfD36E2c2a6789Db23113685031d7F16329158384', 'Venus BNB Pool', 2, 1, GETUTCDATE(), NULL);

-- POLYGON - Super Active Addresses
INSERT INTO MonitoredWallets (Address, Label, BlockchainNetworkId, IsActive, CreatedAt, LastCheckedAt)
VALUES
    -- Uniswap V3 on Polygon - Very active
    ('0xE592427A0AEce92De3Edee1F18E0157C05861564', 'Uniswap V3 Polygon', 3, 1, GETUTCDATE(), NULL),

    -- Polygon Bridge - Constant bridging
    ('0xA0c68C638235ee32657e8f720a23ceC1bFc77C77', 'Polygon Bridge', 3, 1, GETUTCDATE(), NULL),

    -- QuickSwap Factory - High DEX volume
    ('0x5757371414417b8C6CAad45bAeF941aBc7d3Ab32', 'QuickSwap Factory', 3, 1, GETUTCDATE(), NULL);

PRINT 'High-activity wallets added successfully!';
PRINT 'Total wallets added: 11 extremely active addresses';
PRINT 'These wallets will generate constant transaction notifications!';
