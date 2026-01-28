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

        public IndexModel(
            ITransactionRepository transactionRepository,
            IWalletRepository walletRepository,
            IMonitoringStatusRepository monitoringStatusRepository)
        {
            _transactionRepository = transactionRepository;
            _walletRepository = walletRepository;
            _monitoringStatusRepository = monitoringStatusRepository;
        }

        public IEnumerable<Transaction> RecentTransactions { get; set; } = new List<Transaction>();
        public int TotalWallets { get; set; }
        public int ActiveWallets { get; set; }
        public int TransactionsToday { get; set; }
        public int WhaleTransactionCount { get; set; }
        public IEnumerable<MonitoringStatus> MonitoringStatuses { get; set; } = new List<MonitoringStatus>();

        [BindProperty(SupportsGet = true)]
        public string? Sort { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Filter { get; set; }

        public async Task OnGetAsync()
        {
            // Get transactions based on filter/sort
            if (Filter == "whales")
            {
                RecentTransactions = await _transactionRepository.GetWhaleTransactionsAsync(50);
            }
            else if (Sort == "whales-first")
            {
                RecentTransactions = await _transactionRepository.GetRecentWithWhalePriorityAsync(50);
            }
            else
            {
                RecentTransactions = await _transactionRepository.GetRecentAsync(50);
            }

            var allWallets = await _walletRepository.GetAllAsync();
            TotalWallets = allWallets.Count();
            ActiveWallets = allWallets.Count(w => w.IsActive);

            TransactionsToday = await _transactionRepository.GetTransactionCountByDateAsync(TimeZoneHelper.GetCentralToday());

            WhaleTransactionCount = await _transactionRepository.GetWhaleTransactionCountAsync();

            MonitoringStatuses = await _monitoringStatusRepository.GetAllAsync();
        }
    }
}
