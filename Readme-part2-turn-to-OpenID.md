# RazorPageShopManager Part 2: Turn This App Into An Identity Provider (Auth API)

Goal:

- keep this app as user management (users/roles/permissions)
- expose a central login page for other applications
- redirect users back with tokens (JWT)
- support consent screen (Allow/Deny)

=> Turn app to an OAuth2/OpenID Connect Authorization Server.

Do not use IdentityServer, the practical choice in .NET is:

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
			.SetEndSessionEndpointUris("/connect/logout");

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
			   .EnableUserInfoEndpointPassthrough()
			   .EnableLogoutEndpointPassthrough()
			   .EnableStatusCodePagesIntegration();
	})
	.AddValidation(options =>
	{
		options.UseLocalServer();
		options.UseAspNetCore();
	});
```

Important note for multiple client applications:

- `options.RegisterScopes(...)` is global server configuration, not per-client configuration.
- Register here the full list of scopes your Identity service is allowed to issue across all systems.
- Each client application must still be registered separately and explicitly granted only the scopes it is allowed to request.
- If you add more frontend systems later, you usually extend the registered scope list only when a new API permission is introduced, not just because a new client exists.

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

### Multi-client guideline

If more than two separate systems use this Identity service, update your design like this:

1. Register one OpenIddict application per client system.
2. Give each client its own exact `RedirectUris` and `PostLogoutRedirectUris`.
3. Give each client only the scopes it actually needs.
4. Use PKCE for public clients (SPA, mobile, desktop).
5. Use a client secret only for confidential clients (server-side MVC, backend web apps).
6. Keep consent per client application, not as one shared consent across all systems.

Example:

```text
Client A: admin-portal
- RedirectUri: https://localhost:5173/callback
- Scopes: openid profile email orders.read orders.write

Client B: reporting-ui
- RedirectUri: https://localhost:5180/callback
- Scopes: openid profile email orders.read

Client C: mobile-app
- RedirectUri: myapp://callback
- Scopes: openid profile email orders.read
```

This means the main update is not in `RegisterScopes` itself, but in how you seed and maintain client registrations:

- `RegisterScopes` defines the universe of supported scopes.
- each seeded client defines which subset of scopes it may request.
- authorization/consent logic should validate the requested scopes against that client's allowed permissions.

Recommended scope design when the number of clients grows:

- Do not create scopes per client unless there is a real business rule requiring it.
- Prefer scopes per API/resource capability, such as `orders.read`, `orders.write`, `catalog.read`, `catalog.write`.
- If different systems use different APIs, let each client request only the scopes for the APIs it actually calls.
- Keep identity scopes (`openid`, `profile`, `email`) separate from API scopes.

---

## 8. Create and register client applications properly

When you say a system "uses your Identity service", in practice you need to create an OpenIddict client application entry for it.

That client entry is where you define:

- who the client is (`ClientId`)
- whether it has a secret (`ClientSecret`)
- where login can redirect back to (`RedirectUris`)
- where logout can redirect back to (`PostLogoutRedirectUris`)
- which flows it can use
- which scopes it may request
- whether user consent is required

### Core client fields

For each client, you should configure these values deliberately:

- `ClientId`: unique public identifier for the client application.
- `ClientSecret`: confidential credential used only by server-side clients. Do not use this for SPA/mobile/public apps.
- `DisplayName`: friendly name shown on consent screens.
- `RedirectUris`: exact callback URLs allowed after successful login.
- `PostLogoutRedirectUris`: exact callback URLs allowed after logout/end-session.
- `Permissions`: allowed endpoints, grant types, response types, and scopes.
- `ConsentType`: whether the user must explicitly allow access.
- `Requirements`: additional security requirements such as PKCE.

### How to choose client type

Use this rule first:

1. Public client: SPA, mobile app, desktop app, or any app that cannot safely store a secret.
2. Confidential client: server-rendered web app, backend service, or daemon that can safely store credentials.

Recommended setup:

- SPA/mobile/desktop:
  - authorization code flow with PKCE
  - no client secret
  - exact redirect URI
  - exact post-logout redirect URI if logout redirect is needed
- Server-side MVC/web app:
  - authorization code flow
  - client secret
  - exact redirect URI
  - exact post-logout redirect URI
- Machine-to-machine service:
  - usually client credentials flow
  - client secret or certificate
  - no browser redirect URI because no interactive login page is involved

### What redirect URI means

`RedirectUri` is the callback endpoint in the client app where the authorization server sends the user after login/consent.

Examples:

- SPA: `https://localhost:5173/callback`
- MVC app: `https://localhost:5001/signin-oidc`
- Mobile app: `myapp://callback`

Important rules:

1. It must match exactly what the client application actually uses.
2. Do not allow wildcards like `https://localhost:5173/*`.
3. Treat redirect URI mismatches as hard failures.
4. For production, prefer HTTPS except for native-app custom URI schemes.

### What post-logout redirect URI means

`PostLogoutRedirectUri` is where the user may be returned after a successful logout or end-session flow.

Examples:

- SPA: `https://localhost:5173/`
- MVC app: `https://localhost:5001/signout-callback-oidc`

Important rules:

1. Register it explicitly, just like login redirect URIs.
2. Do not redirect to arbitrary URLs after logout.
3. If your client does not implement logout callback handling, this can be omitted.

### What client ID and secret mean

- `ClientId` is not a password. It identifies the client.
- `ClientSecret` is a credential and must be stored securely.
- Public clients should not receive a secret because it cannot be protected.
- Confidential clients should keep the secret in secure configuration, not in source code.

### Minimal seeding example

Typical startup seeding should create clients explicitly. Conceptually, you seed something like this:

```csharp
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

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
					Permissions.Scopes.OpenId,
					Permissions.Scopes.Profile,
					Permissions.Scopes.Email,
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
					Permissions.Scopes.OpenId,
					Permissions.Scopes.Profile,
					Permissions.Scopes.Email,
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
```

### Example: confidential server-side client

For a confidential MVC app, add a client secret and usually use the standard OpenID Connect callback routes:

```text
ClientId: backoffice-mvc
ClientSecret: [secure generated secret]
RedirectUri: https://localhost:5001/signin-oidc
PostLogoutRedirectUri: https://localhost:5001/signout-callback-oidc
Scopes: openid profile email orders.read
Flow: authorization_code
PKCE: recommended
Client type: confidential
```

### Extra things that are necessary in practice

These are the parts many first implementations miss:

1. Give every client a stable `DisplayName` because users will see it on the consent page.
2. Never share one `ClientId` across multiple systems.
3. Keep development and production redirect URIs separate.
4. If a client has multiple environments, register each environment URI intentionally.
5. Re-check that the client requests only scopes it is permitted to use.
6. If you support refresh tokens, decide per client whether that is allowed.
7. Use explicit consent for third-party or high-risk clients.
8. Log failed authorize/token requests because redirect mismatch and invalid scope issues are common.
9. Document ownership of each client registration: which team owns it, which app it maps to, and which scopes it is approved for.

### Suggested registration checklist per client

Before adding a new client, collect this information:

1. Application name
2. Environment (`dev`, `test`, `prod`)
3. Client type (`public` or `confidential`)
4. Login redirect URI(s)
5. Logout redirect URI(s)
6. Required scopes
7. Whether refresh tokens are needed
8. Whether consent must be explicit
9. Team owner/contact

With that information, you can seed the client safely and keep the setup auditable.

---

## 9. Add migration and update database

After adding OpenIddict + Identity model changes:

```powershell
dotnet ef migrations add AddIdentityAndOpenIddict --project .\RazorPageShopManager
dotnet ef database update --project .\RazorPageShopManager
```

You should now have both Identity and OpenIddict tables.

---

## 10. Configure another service (Order Service) to accept tokens

Order Service should trust this auth server as issuer and validate JWT.

In Order Service API:

1. Add JWT bearer authentication.
2. Set `Authority` to this Auth Server base URL.
3. Set expected audience/scope policy.

Example policy idea:

- `orders.read` required for GET orders
- `orders.write` required for create/update orders

---

## 11. Configure frontend client app (redirect to auth server)

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

## 12. User management and permissions in the future

Keep this project as central IAM admin portal:

1. Manage users and roles via Identity UI/admin pages.
2. Manage app permissions as OAuth scopes (`orders.read`, `orders.write`, etc.).
3. Map roles/claims to scope access rules in authorize/token logic.
4. Keep consent records in OpenIddict authorizations.

Recommended model:

- Roles: coarse-grained business grouping (`Admin`, `Manager`, `Staff`).
- Scopes/permissions: API access contracts for external services.

---

## 13. Production readiness checklist

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

## 14. Minimal practical rollout order

1. Finish Part 1 Identity migration.
2. Add OpenIddict packages and configuration.
3. Build authorize/token endpoints and consent page.
4. Seed one test client app.
5. Connect one frontend app with auth code + PKCE.
6. Protect one API (Order Service) with JWT validation.
7. Add role/scope policies and admin management pages.

This is the clean path to evolve this project from local user management into a central identity provider for your microservices/frontends.
