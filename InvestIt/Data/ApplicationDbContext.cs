using InvestIt.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestIt.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<BlockchainNetwork> BlockchainNetworks { get; set; }
    public DbSet<MonitoredWallet> MonitoredWallets { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<NotificationQueue> NotificationQueues { get; set; }
    public DbSet<ApiRateLimitTracking> ApiRateLimitTrackings { get; set; }
    public DbSet<MonitoringStatus> MonitoringStatuses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // BlockchainNetwork configuration
        modelBuilder.Entity<BlockchainNetwork>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ChainId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ApiUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ApiKey).HasMaxLength(500);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.ChainId).IsUnique();
        });

        // MonitoredWallet configuration
        modelBuilder.Entity<MonitoredWallet>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Address).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Label).HasMaxLength(200);

            // Unique constraint on Address and BlockchainNetworkId
            entity.HasIndex(e => new { e.Address, e.BlockchainNetworkId }).IsUnique();
            entity.HasIndex(e => e.BlockchainNetworkId);
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.BlockchainNetwork)
                .WithMany(n => n.MonitoredWallets)
                .HasForeignKey(e => e.BlockchainNetworkId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Transaction configuration
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TxHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.FromAddress).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ToAddress).IsRequired().HasMaxLength(255);
            entity.Property(e => e.TokenSymbol).HasMaxLength(50);
            entity.Property(e => e.TokenAddress).HasMaxLength(255);
            entity.Property(e => e.Amount).HasPrecision(38, 18);
            entity.Property(e => e.AmountUsd).HasPrecision(18, 2);
            entity.Property(e => e.GasUsed).HasPrecision(38, 18);
            entity.Property(e => e.GasPrice).HasPrecision(38, 18);

            // Indexes for performance
            entity.HasIndex(e => e.TxHash).IsUnique();
            entity.HasIndex(e => new { e.WalletId, e.Timestamp });
            entity.HasIndex(e => e.TokenSymbol);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.AmountUsd);

            entity.HasOne(e => e.Wallet)
                .WithMany(w => w.Transactions)
                .HasForeignKey(e => e.WalletId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // NotificationQueue configuration
        modelBuilder.Entity<NotificationQueue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.HasIndex(e => e.TransactionId);

            entity.HasOne(e => e.Transaction)
                .WithMany(t => t.NotificationQueues)
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ApiRateLimitTracking configuration
        modelBuilder.Entity<ApiRateLimitTracking>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.NetworkId).IsUnique();

            entity.HasOne(e => e.Network)
                .WithMany(n => n.ApiRateLimitTrackings)
                .HasForeignKey(e => e.NetworkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // MonitoringStatus configuration
        modelBuilder.Entity<MonitoringStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.NetworkId).IsUnique();
            entity.HasIndex(e => e.IsHealthy);

            entity.HasOne(e => e.Network)
                .WithMany(n => n.MonitoringStatuses)
                .HasForeignKey(e => e.NetworkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed initial blockchain networks
        var seedDate = new DateTime(2026, 1, 25, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<BlockchainNetwork>().HasData(
            new BlockchainNetwork
            {
                Id = 1,
                Name = "Ethereum",
                ChainId = "1",
                ApiUrl = "https://api.etherscan.io/api",
                RateLimitPerSecond = 5,
                CreatedAt = seedDate
            },
            new BlockchainNetwork
            {
                Id = 2,
                Name = "Binance Smart Chain",
                ChainId = "56",
                ApiUrl = "https://api.bscscan.com/api",
                RateLimitPerSecond = 5,
                CreatedAt = seedDate
            },
            new BlockchainNetwork
            {
                Id = 3,
                Name = "Polygon",
                ChainId = "137",
                ApiUrl = "https://api.polygonscan.com/api",
                RateLimitPerSecond = 5,
                CreatedAt = seedDate
            },
            new BlockchainNetwork
            {
                Id = 4,
                Name = "Solana",
                ChainId = "solana-mainnet",
                ApiUrl = "https://api.solscan.io",
                RateLimitPerSecond = 3,
                CreatedAt = seedDate
            }
        );
    }
}
