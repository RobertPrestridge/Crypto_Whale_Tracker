using InvestIt.Data.Entities;
using InvestIt.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InvestIt.Pages.Wallets
{
    public class DetailsModel : PageModel
    {
        private readonly IWalletRepository _walletRepository;
        private readonly ITransactionRepository _transactionRepository;

        public DetailsModel(
            IWalletRepository walletRepository,
            ITransactionRepository transactionRepository)
        {
            _walletRepository = walletRepository;
            _transactionRepository = transactionRepository;
        }

        public MonitoredWallet? Wallet { get; set; }
        public IEnumerable<Transaction> Transactions { get; set; } = new List<Transaction>();
        public int TotalTransactions { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            Wallet = await _walletRepository.GetByIdAsync(id);

            if (Wallet == null)
            {
                return NotFound();
            }

            Transactions = await _transactionRepository.GetByWalletAsync(id, 0, 100);
            TotalTransactions = await _transactionRepository.GetCountByWalletAsync(id);

            return Page();
        }
    }
}
