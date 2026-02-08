using InvestIt.Data.Entities;
using InvestIt.Helpers;
using InvestIt.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InvestIt.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IWalletRepository _walletRepository;
        private readonly IMonitoringStatusRepository _monitoringStatusRepository;
        private readonly IConfiguration _configuration;

        public IndexModel(
            ITransactionRepository transactionRepository,
            IWalletRepository walletRepository,
            IMonitoringStatusRepository monitoringStatusRepository,
            IConfiguration configuration)
        {
            _transactionRepository = transactionRepository;
            _walletRepository = walletRepository;
            _monitoringStatusRepository = monitoringStatusRepository;
            _configuration = configuration;
        }

        public IEnumerable<Transaction> RecentTransactions { get; set; } = new List<Transaction>();
        public int TotalWallets { get; set; }
        public int ActiveWallets { get; set; }
        public int TransactionsToday { get; set; }
        public int WhaleTransactionCount { get; set; }
        public decimal MinAmountUsd { get; set; }
        public IEnumerable<MonitoringStatus> MonitoringStatuses { get; set; } = new List<MonitoringStatus>();

        [BindProperty(SupportsGet = true)]
        public string? Sort { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Filter { get; set; }

        public async Task OnGetAsync()
        {
            var maxDisplay = _configuration.GetValue<int>("DashboardSettings:MaxTransactionsDisplayed", 50);
            MinAmountUsd = _configuration.GetValue<decimal>("DashboardSettings:MinAmountUsd", 10000);

            // All dashboard views filter to transactions >= $10,000 USD
            if (Filter == "whales")
            {
                RecentTransactions = await _transactionRepository.GetWhaleTransactionsAsync(maxDisplay);
            }
            else if (Sort == "whales-first")
            {
                RecentTransactions = await _transactionRepository.GetRecentWithWhalePriorityAsync(maxDisplay);
            }
            else
            {
                RecentTransactions = await _transactionRepository.GetRecentAboveUsdAsync(MinAmountUsd, maxDisplay);
            }

            var allWallets = await _walletRepository.GetAllAsync();
            TotalWallets = allWallets.Count();
            ActiveWallets = allWallets.Count(w => w.IsActive);

            TransactionsToday = await _transactionRepository.GetTransactionCountByDateAsync(TimeZoneHelper.GetCentralToday());

            WhaleTransactionCount = await _transactionRepository.GetWhaleTransactionCountByUsdAsync(MinAmountUsd);

            MonitoringStatuses = await _monitoringStatusRepository.GetAllAsync();
        }
    }
}
