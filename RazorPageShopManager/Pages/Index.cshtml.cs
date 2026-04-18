using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RazorPageShopManager.Entities;
using System.Security.Claims;

namespace RazorPageShopManager.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        public ApplicationUser? CurrentUser;
        public List<Claim> Claims;
        public string RoleName;

        public IndexModel(ILogger<IndexModel> logger, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _userManager = userManager;
        }

        public async Task OnGet()
        {
            CurrentUser = await _userManager.GetUserAsync(User);
            if (CurrentUser == null)
            {
                RedirectToAction("Login", "Account");
                return;
            }

            var userClaims = await _userManager.GetClaimsAsync(CurrentUser);
            if (userClaims != null)
            {
                Claims = userClaims.ToList();
            }

            var userRoles = await _userManager.GetRolesAsync(CurrentUser);
            if (userRoles != null && userRoles.Count > 0)
            {
                RoleName = string.Join(", ", userRoles);
            }
        }

        public async Task<IActionResult> OnPostRole()
        {
            CurrentUser = await _userManager.GetUserAsync(User);
            if (CurrentUser == null)
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var selectedRole = Request.Form["roleName"];
            if (!string.IsNullOrEmpty(selectedRole))
            {
                await _userManager.AddToRoleAsync(CurrentUser, selectedRole);
            }
            await _userManager.UpdateSecurityStampAsync(CurrentUser);
            return RedirectToPage("/Index");
        }
    }
}
