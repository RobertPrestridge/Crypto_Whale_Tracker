using InvestIt.BackgroundServices;
using InvestIt.Data;
using InvestIt.Hubs;
using InvestIt.Middleware;
using InvestIt.Repositories.Implementations;
using InvestIt.Repositories.Interfaces;
using InvestIt.Services.ExplorerClients;
using InvestIt.Services.Interfaces;
using InvestIt.Services.Monitoring;
using InvestIt.Services.Notifications;
using InvestIt.Services.PriceTracking;
using InvestIt.Services.Processing;
using InvestIt.Services.RateLimiting;
using InvestIt.Services.WhaleTracking;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using Refit;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/investit-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register repositories
builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IBlockchainNetworkRepository, BlockchainNetworkRepository>();
builder.Services.AddScoped<INotificationQueueRepository, NotificationQueueRepository>();
builder.Services.AddScoped<IMonitoringStatusRepository, MonitoringStatusRepository>();

// Create Polly policies
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1));

var combinedPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

// Register Refit API client for Etherscan V2 unified API
// V2 uses a single endpoint with chainid parameter for all EVM chains
builder.Services.AddRefitClient<IEtherscanApi>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(builder.Configuration["BlockchainApis:Ethereum:ApiUrl"] ?? "https://api.etherscan.io/v2/api"))
    .AddPolicyHandler(combinedPolicy);

// Register blockchain explorer clients
builder.Services.AddScoped<EtherscanClient>();
builder.Services.AddScoped<BscScanClient>();
builder.Services.AddScoped<PolygonscanClient>();
builder.Services.AddScoped<SolanaClient>();

// Register core services
builder.Services.AddSingleton<IRateLimitService, RateLimitService>();
builder.Services.AddScoped<ITransactionProcessingService, TransactionProcessingService>();
builder.Services.AddScoped<INotificationService, SignalRNotificationService>();

// Register price tracking services
builder.Services.AddMemoryCache(); // For price caching
builder.Services.AddHttpClient<ICoinGeckoClient, CoinGeckoClient>();

// Register whale tracking
builder.Services.AddScoped<WhaleTransactionMonitorService>();
builder.Services.AddScoped<GlobalWhaleMonitorService>();

// Register chain monitor services
builder.Services.AddScoped<IChainMonitorService, EthereumMonitorService>();
builder.Services.AddScoped<IChainMonitorService, BscMonitorService>();
builder.Services.AddScoped<IChainMonitorService, PolygonMonitorService>();
builder.Services.AddScoped<IChainMonitorService, SolanaMonitorService>();
builder.Services.AddScoped<IBlockchainMonitoringService, BlockchainMonitoringService>();

// Register background services
builder.Services.AddHostedService<BlockchainMonitoringHostedService>();
builder.Services.AddHostedService<NotificationProcessorHostedService>();

// Add SignalR
builder.Services.AddSignalR();

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// Map SignalR hub
app.MapHub<TransactionNotificationHub>("/hubs/transactions");

// Map health check endpoint
app.MapHealthChecks("/health");

app.Run();
