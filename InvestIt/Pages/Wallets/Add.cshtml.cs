using InvestIt.Data.Entities;
using InvestIt.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InvestIt.Pages.Wallets
{
    public class AddModel : PageModel
    {
        private readonly IWalletRepository _walletRepository;
        private readonly IBlockchainNetworkRepository _networkRepository;

        public AddModel(
            IWalletRepository walletRepository,
            IBlockchainNetworkRepository networkRepository)
        {
            _walletRepository = walletRepository;
            _networkRepository = networkRepository;
        }

        [BindProperty]
        public string Address { get; set; } = string.Empty;

        [BindProperty]
        public int BlockchainNetworkId { get; set; }

        [BindProperty]
        public string? Label { get; set; }

        public List<SelectListItem> Networks { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LoadNetworks();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadNetworks();
                return Page();
            }

            // Check if wallet already exists
            if (await _walletRepository.ExistsAsync(Address, BlockchainNetworkId))
            {
                ModelState.AddModelError(string.Empty, "This wallet is already being monitored.");
                await LoadNetworks();
                return Page();
            }

            var wallet = new MonitoredWallet
            {
                Address = Address,
                BlockchainNetworkId = BlockchainNetworkId,
                Label = Label,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _walletRepository.AddAsync(wallet);

            TempData["SuccessMessage"] = "Wallet added successfully and will be monitored!";
            return RedirectToPage("List");
        }

        private async Task LoadNetworks()
        {
            var networks = await _networkRepository.GetActiveAsync();
            Networks = networks.Select(n => new SelectListItem
            {
                Value = n.Id.ToString(),
                Text = n.Name
            }).ToList();
        }
    }
}
