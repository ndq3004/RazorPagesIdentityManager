# RazorPageShopManager Part 2: Turn This App Into An Identity Provider (Auth API)

Yes, this is possible.

If your goal is:

- keep this app as user management (users/roles/permissions)
- expose a central login page for other applications
- redirect users back with tokens (JWT)
- support consent screen (Allow/Deny)

then your app needs to become an OAuth2/OpenID Connect Authorization Server.

Since you do not want IdentityServer, the practical choice in .NET is:

- ASP.NET Core Identity for user accounts, roles, and local login UI
- OpenIddict for OAuth2/OIDC endpoints and token issuance

---

## 0. Architecture target (what you are building)

Components:

1. Auth Server (this project): login, consent, token issuance.
2. Client Apps (frontend apps): redirect users to Auth Server.
3. Resource APIs (Order Service, etc.): validate access tokens from Auth Server.

Flow:

1. User opens client app.
2. Client redirects to `/connect/authorize` on this project with `client_id`, `redirect_uri`, `scope`, `code_challenge`, etc.
3. If not logged in, user is sent to login page.
4. After login, consent page is shown (Allow / Deny).
5. On Allow, Auth Server returns an authorization code to client redirect URI.
6. Client exchanges code at `/connect/token`.
7. Auth Server returns tokens (ID token + access token JWT + optional refresh token).
8. Client calls Order Service with bearer access token.

---

## 1. Add required packages

Install OpenIddict packages in this project:

```powershell
dotnet add .\RazorPageShopManager\package OpenIddict.AspNetCore
dotnet add .\RazorPageShopManager\package OpenIddict.EntityFrameworkCore
dotnet add .\RazorPageShopManager\package OpenIddict.Server.AspNetCore
dotnet add .\RazorPageShopManager\package OpenIddict.Validation.AspNetCore
```

Keep your existing ASP.NET Core Identity + EF Core packages from Part 1.

---

## 2. Use one DbContext for Identity + OpenIddict

Keep a single `AppDbContext` (recommended for this project) and register OpenIddict EF entities in it.

In `Program.cs`, configure DbContext like:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
{
	options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
	options.UseOpenIddict();
});
```

This lets one database store:

- ASP.NET Identity tables (`AspNetUsers`, `AspNetRoles`, ...)
- OpenIddict tables (applications, authorizations, scopes, tokens)

---

## 3. Configure ASP.NET Identity (users/roles/policies)

Keep Identity as your user store and role manager:

```csharp
builder.Services
	.AddDefaultIdentity<ApplicationUser>(options =>
	{
		options.SignIn.RequireConfirmedAccount = false;
	})
	.AddRoles<IdentityRole>()
	.AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddAuthorization(options =>
{
	options.AddPolicy("CanManageUsers", p => p.RequireRole("Admin"));
});
```

Identity handles user/role management pages; OpenIddict handles OAuth2/OIDC protocol endpoints.

---

## 4. Configure OpenIddict server endpoints and flows

In `Program.cs`, add OpenIddict setup:

```csharp
using OpenIddict.Abstractions;

builder.Services.AddOpenIddict()
	.AddCore(options =>
	{
		options.UseEntityFrameworkCore()
			   .UseDbContext<AppDbContext>();
	})
	.AddServer(options =>
	{
		options
			.SetAuthorizationEndpointUris("/connect/authorize")
			.SetTokenEndpointUris("/connect/token")
			.SetUserinfoEndpointUris("/connect/userinfo")
			.SetLogoutEndpointUris("/connect/logout");

		options.AllowAuthorizationCodeFlow()
			   .RequireProofKeyForCodeExchange();

		options.AllowRefreshTokenFlow();

		options.RegisterScopes(
			OpenIddictConstants.Scopes.OpenId,
			OpenIddictConstants.Scopes.Profile,
			OpenIddictConstants.Scopes.Email,
			"orders.read",
			"orders.write");

		options.AddDevelopmentEncryptionCertificate()
			   .AddDevelopmentSigningCertificate();

		options.UseAspNetCore()
			   .EnableAuthorizationEndpointPassthrough()
			   .EnableTokenEndpointPassthrough()
			   .EnableUserinfoEndpointPassthrough()
			   .EnableLogoutEndpointPassthrough()
			   .EnableStatusCodePagesIntegration();
	})
	.AddValidation(options =>
	{
		options.UseLocalServer();
		options.UseAspNetCore();
	});
```

For production, replace development certs with real persisted signing/encryption certificates.

---

## 5. Add authentication/authorization middleware

In HTTP pipeline:

```csharp
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
```

Order matters. Keep authentication before authorization.

---

## 6. Create OAuth/OIDC endpoints with login + consent flow

You need endpoint handlers for:

- `/connect/authorize` (GET/POST)
- `/connect/token` (POST)

The behavior should be:

1. If user not authenticated, challenge and redirect to Identity login.
2. If authenticated but no prior consent, show consent page.
3. If user clicks Allow, issue authorization code.
4. Client exchanges code for JWT tokens at token endpoint.

Implementation approaches:

1. Build a dedicated controller (recommended) with OpenIddict request parsing.
2. Or use Razor Page handlers if you prefer page-first style.

For consent, create a page (for example `/Consent`) showing:

- client name
- requested scopes
- Allow / Deny actions

When allowed, create a `ClaimsPrincipal` and call `SignIn(...)` using OpenIddict server scheme.

---

## 7. Register client applications (clientId, redirect URIs, consent type)

Create a startup seeder to register clients in OpenIddict application store.

For each client app, configure:

- `ClientId` (required)
- `ClientSecret` (for confidential clients)
- `RedirectUris` (must be exact)
- `PostLogoutRedirectUris`
- allowed grant type (`authorization_code`)
- PKCE requirement
- allowed scopes
- consent type (`Explicit` if you want Allow/Deny page)

Example client shape:

```text
ClientId: web-frontend
RedirectUri: https://localhost:5173/callback
PostLogoutRedirectUri: https://localhost:5173/
Scopes: openid profile email orders.read
ConsentType: Explicit
```

If `ConsentType` is explicit, your authorize flow will show confirmation page before issuing code.

---

## 8. Add migration and update database

After adding OpenIddict + Identity model changes:

```powershell
dotnet ef migrations add AddIdentityAndOpenIddict --project .\RazorPageShopManager
dotnet ef database update --project .\RazorPageShopManager
```

You should now have both Identity and OpenIddict tables.

---

## 9. Configure another service (Order Service) to accept tokens

Order Service should trust this auth server as issuer and validate JWT.

In Order Service API:

1. Add JWT bearer authentication.
2. Set `Authority` to this Auth Server base URL.
3. Set expected audience/scope policy.

Example policy idea:

- `orders.read` required for GET orders
- `orders.write` required for create/update orders

---

## 10. Configure frontend client app (redirect to auth server)

In client frontend app (SPA or MVC):

1. Configure OIDC authority = Auth Server URL.
2. Configure `client_id`, `redirect_uri`, `response_type=code`.
3. Enable PKCE.
4. Request scopes: `openid profile email orders.read`.
5. Handle callback route and store tokens securely.

This gives you the exact behavior you described:

- not logged in -> redirected to auth server login
- after login -> consent screen
- click accept -> redirected back with code
- app exchanges code -> receives JWT access token

---

## 11. User management and permissions in the future

Keep this project as central IAM admin portal:

1. Manage users and roles via Identity UI/admin pages.
2. Manage app permissions as OAuth scopes (`orders.read`, `orders.write`, etc.).
3. Map roles/claims to scope access rules in authorize/token logic.
4. Keep consent records in OpenIddict authorizations.

Recommended model:

- Roles: coarse-grained business grouping (`Admin`, `Manager`, `Staff`).
- Scopes/permissions: API access contracts for external services.

---

## 12. Production readiness checklist

Before production, complete these:

1. Use real signing/encryption certificates (not development certs).
2. Enforce HTTPS everywhere.
3. Strictly validate redirect URIs.
4. Use short access token lifetime, refresh token rotation.
5. Add audit logs for login, consent, token issuance, role changes.
6. Add anti-forgery + secure cookie settings.
7. Add key rotation strategy.
8. Add rate limits and brute-force protection.

---

## 13. Minimal practical rollout order

1. Finish Part 1 Identity migration.
2. Add OpenIddict packages and configuration.
3. Build authorize/token endpoints and consent page.
4. Seed one test client app.
5. Connect one frontend app with auth code + PKCE.
6. Protect one API (Order Service) with JWT validation.
7. Add role/scope policies and admin management pages.

This is the clean path to evolve this project from local user management into a central identity provider for your microservices/frontends.
