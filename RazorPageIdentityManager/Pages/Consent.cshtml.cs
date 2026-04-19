using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorPageIdentityManager.Pages
{
    [Authorize]
    public class ConsentModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; } = "/";
        [BindProperty(SupportsGet = true)]
        public string ClientId { get; set; } = "unknown-client";
        [BindProperty(SupportsGet = true)]
        public string Scope { get; set; } = "";
        public IActionResult OnGet()
        {
            if (string.IsNullOrEmpty(ReturnUrl))
            {
                return BadRequest("ReturnUrl is required");
            }

            return Page();
        }
    }
}
