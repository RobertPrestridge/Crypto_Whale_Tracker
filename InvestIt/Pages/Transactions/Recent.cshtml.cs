using InvestIt.Data.Entities;
using InvestIt.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InvestIt.Pages.Transactions
{
    public class RecentModel : PageModel
    {
        private readonly ITransactionRepository _transactionRepository;

        public RecentModel(ITransactionRepository transactionRepository)
        {
            _transactionRepository = transactionRepository;
        }

        public IEnumerable<Transaction> Transactions { get; set; } = new List<Transaction>();
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 50;

        public async Task OnGetAsync(int? page)
        {
            CurrentPage = page ?? 1;
            var skip = (CurrentPage - 1) * PageSize;

            Transactions = await _transactionRepository.GetRecentAsync(PageSize);
        }
    }
}
