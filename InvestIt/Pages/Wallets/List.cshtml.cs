using InvestIt.Data.Entities;
using InvestIt.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InvestIt.Pages.Wallets
{
    public class ListModel : PageModel
    {
        private readonly IWalletRepository _walletRepository;

        public ListModel(IWalletRepository walletRepository)
        {
            _walletRepository = walletRepository;
        }

        public IEnumerable<MonitoredWallet> Wallets { get; set; } = new List<MonitoredWallet>();

        public async Task OnGetAsync()
        {
            Wallets = await _walletRepository.GetAllAsync();
        }

        public async Task<IActionResult> OnPostToggleActiveAsync(int id)
        {
            var wallet = await _walletRepository.GetByIdAsync(id);
            if (wallet != null)
            {
                wallet.IsActive = !wallet.IsActive;
                await _walletRepository.UpdateAsync(wallet);
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            await _walletRepository.DeleteAsync(id);
            TempData["SuccessMessage"] = "Wallet deleted successfully.";
            return RedirectToPage();
        }
    }
}
