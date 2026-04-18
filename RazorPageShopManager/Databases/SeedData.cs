using Microsoft.AspNetCore.Identity;

namespace RazorPageShopManager.Databases
{
    public static class SeedData
    {
        public static readonly string[] Roles = ["Admin", "Manager", "Staff"];

        public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            foreach (var role in Roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
    }
}
