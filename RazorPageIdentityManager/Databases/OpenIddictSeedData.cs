using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace RazorPageIdentityManager.Databases
{
    public static class OpenIddictSeedData
    {
        public static async Task SeedClientsAsync(IServiceProvider serviceProvider)
        {
            var manager = serviceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            if (await manager.FindByClientIdAsync("admin-portal") is null)
            {
                await manager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "admin-portal",
                    DisplayName = "Admin Portal",
                    ConsentType = ConsentTypes.Explicit,
                    RedirectUris =
                    {
                        new Uri("https://localhost:5173/callback")
                    },
                    PostLogoutRedirectUris =
                    {
                        new Uri("https://localhost:5173/")
                    },
                    Permissions =
                    {
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.Token,
                        Permissions.Endpoints.EndSession,
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.ResponseTypes.Code,
                        Permissions.Prefixes.Scope + Scopes.OpenId,
                        Permissions.Prefixes.Scope + Scopes.Profile,
                        Permissions.Prefixes.Scope + Scopes.Email,
                        Permissions.Prefixes.Scope + "orders.read",
                        Permissions.Prefixes.Scope + "orders.write"
                    },
                    Requirements =
                    {
                        Requirements.Features.ProofKeyForCodeExchange
                    }
                });
            }

            if (await manager.FindByClientIdAsync("reporting-ui") is null)
            {
                await manager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "reporting-ui",
                    DisplayName = "Reporting UI",
                    ConsentType = ConsentTypes.Explicit,
                    RedirectUris =
                    {
                        new Uri("https://localhost:5180/callback")
                    },
                    PostLogoutRedirectUris =
                    {
                        new Uri("https://localhost:5180/")
                    },
                    Permissions =
                    {
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.Token,
                        Permissions.Endpoints.EndSession,
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.ResponseTypes.Code,
                        Permissions.Prefixes.Scope + Scopes.OpenId,
                        Permissions.Prefixes.Scope + Scopes.Profile,
                        Permissions.Prefixes.Scope + Scopes.Email,
                        Permissions.Prefixes.Scope + "orders.read"
                    },
                    Requirements =
                    {
                        Requirements.Features.ProofKeyForCodeExchange
                    }
                });
            }

            if (await manager.FindByClientIdAsync("simple-spa-test") is null)
            {
                await manager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "simple-spa-test",
                    DisplayName = "Test simple app",
                    ConsentType = ConsentTypes.Explicit,
                    RedirectUris =
                    {
                        new Uri("http://localhost:5500/callback.html")
                    },
                    PostLogoutRedirectUris =
                    {
                        new Uri("http://localhost:5500/")
                    },
                    Permissions =
                    {
                        Permissions.Endpoints.Authorization,
                        Permissions.Endpoints.Token,
                        Permissions.Endpoints.EndSession,
                        Permissions.GrantTypes.AuthorizationCode,
                        Permissions.ResponseTypes.Code,
                        Permissions.Prefixes.Scope + Scopes.OpenId,
                        Permissions.Prefixes.Scope + Scopes.Profile,
                        Permissions.Prefixes.Scope + Scopes.Email,
                        Permissions.Prefixes.Scope + "orders.read"
                    },
                    Requirements =
                    {
                        Requirements.Features.ProofKeyForCodeExchange
                    }
                });
            }
        }
    }
}