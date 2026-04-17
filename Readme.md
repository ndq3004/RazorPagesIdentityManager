# RazorPageShopManager: Convert to ASP.NET Core Identity

This project is currently a plain Razor Pages application with Entity Framework Core and a custom `Login` page. If you want to turn it into an ASP.NET Core Identity project without using IdentityServer, the correct approach is to use the built-in ASP.NET Core Identity system with cookie authentication.

IdentityServer is used for OAuth2/OpenID Connect token issuance for distributed clients and APIs. A Razor Pages app like this one does not need that just to support login, logout, registration, roles, and authorization.

This guide shows the steps to migrate the current project.

---

## 1. Install the required packages

Add the Identity and EF Core design packages to `RazorPageShopManager.csproj`.

Use versions that match your .NET 8 application. Your project currently targets `net8.0`, so keep the EF Core packages on `8.x` as well.

Recommended package list:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="8.0.26" />
  <PackageReference Include="Microsoft.AspNetCore.Identity.UI" Version="8.0.26" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.26">
	<PrivateAssets>all</PrivateAssets>
	<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.26" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.26">
	<PrivateAssets>all</PrivateAssets>
	<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

Notes:

- `Microsoft.EntityFrameworkCore.Tools` is currently version `10.0.6` in this project. That does not match `net8.0` and should be aligned to `8.x`.
- `Microsoft.AspNetCore.Identity.UI` gives you the built-in Identity Razor Pages UI.
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` gives you Identity tables through EF Core.

You can install with CLI:

```powershell
dotnet add .\RazorPageShopManager\package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 8.0.26
dotnet add .\RazorPageShopManager\package Microsoft.AspNetCore.Identity.UI --version 8.0.26
dotnet add .\RazorPageShopManager\package Microsoft.EntityFrameworkCore.Design --version 8.0.26
dotnet add .\RazorPageShopManager\package Microsoft.EntityFrameworkCore.Tools --version 8.0.26
```

---

## 2. Create an application user class

Create a user entity that inherits from `IdentityUser`.

Example file: `Entities/ApplicationUser.cs`

```csharp
using Microsoft.AspNetCore.Identity;

namespace RazorPageShopManager.Entities
{
	public class ApplicationUser : IdentityUser
	{
	}
}
```

Use this class if you later want to add custom fields such as:

- `FullName`
- `Address`
- `CreatedAt`

If you do not need extra fields, you can still keep this class for future growth.

---

## 3. Change `AppDbContext` to use Identity

Your current `AppDbContext` inherits from `DbContext`. It must inherit from `IdentityDbContext<ApplicationUser>` instead.

Update `Databases/AppContext.cs` like this:

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RazorPageShopManager.Databases.EntityTypeConfigurations;
using RazorPageShopManager.Entities;

namespace RazorPageShopManager.Databases
{
	public class AppDbContext : IdentityDbContext<ApplicationUser>
	{
		public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
		{
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.ApplyConfiguration(new ProductConfiguration());
		}
	}
}
```

Why this matters:

- Identity needs its own tables such as `AspNetUsers`, `AspNetRoles`, and `AspNetUserRoles`.
- By inheriting from `IdentityDbContext<ApplicationUser>`, those tables are included in your EF Core model.
- Your existing product configuration can remain in the same context.

---

## 4. Configure Identity in `Program.cs`

Your current `Program.cs` only registers Razor Pages and `AppDbContext`. Identity also needs authentication and cookie middleware.

Replace the setup with this pattern:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RazorPageShopManager.Databases;
using RazorPageShopManager.Entities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
	options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
	.AddDefaultIdentity<ApplicationUser>(options =>
	{
		options.SignIn.RequireConfirmedAccount = false;
		options.Password.RequireDigit = true;
		options.Password.RequireLowercase = true;
		options.Password.RequireUppercase = false;
		options.Password.RequireNonAlphanumeric = false;
		options.Password.RequiredLength = 6;
	})
	.AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
```

Important points:

- `AddDefaultIdentity<ApplicationUser>()` configures the standard Identity system for a Razor Pages app.
- `AddEntityFrameworkStores<AppDbContext>()` stores users and passwords in SQL Server.
- `app.UseAuthentication()` must be added before `app.UseAuthorization()`.

If you want roles too, use this instead:

```csharp
builder.Services
	.AddDefaultIdentity<ApplicationUser>(options =>
	{
		options.SignIn.RequireConfirmedAccount = false;
	})
	.AddRoles<IdentityRole>()
	.AddEntityFrameworkStores<AppDbContext>();
```

Use roles if you plan to support users such as `Admin`, `Manager`, or `Staff`.

### Note: Using both roles and policies

If you want both roles and permission-based authorization (policies), add `AddAuthorization` alongside `AddRoles`. No extra packages are needed — both are part of the standard ASP.NET Core Identity stack.

```csharp
builder.Services
    .AddDefaultIdentity<ApplicationUser>(options => { ... })
    .AddRoles<IdentityRole>()                           // enables roles
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddAuthorization(options =>            // enables policies
{
    options.AddPolicy("CanManageProducts", policy =>
        policy.RequireRole("Admin", "Manager"));

    options.AddPolicy("StaffAndAbove", policy =>
        policy.RequireRole("Admin", "Manager", "Staff"));
});
```

There are two common policy approaches:

**Option A — Claim-based policies**
Store a `Permission` claim directly on a user and require it in a policy. Flexible but requires managing claims per user via `UserManager.AddClaimAsync`.

```csharp
options.AddPolicy("CanManageProducts", policy =>
    policy.RequireClaim("Permission", "Products.Manage"));
```

**Option B — Role + policy hybrid (recommended for this project)**
Define named policies that map to one or more roles. Readable, testable, and no per-user claim management needed. This works well for a shop manager app with roles like `Admin`, `Manager`, and `Staff`.

Use a policy on a page model:

```csharp
[Authorize(Policy = "CanManageProducts")]
public class ProductsModel : PageModel { ... }
```

Or for a simple role check:

```csharp
[Authorize(Roles = "Admin")]
```

---

## 5. Scaffold the Identity UI pages

To make the project feel like a real Identity project, you normally scaffold the Identity pages into your app so you can edit them.

Typical pages include:

- `/Identity/Account/Login`
- `/Identity/Account/Register`
- `/Identity/Account/Logout`
- `/Identity/Account/Manage`

There are two common ways to scaffold them.

### Option A: Visual Studio scaffolder

1. Right-click the project.
2. Choose `Add` -> `New Scaffolded Item`.
3. Choose `Identity`.
4. Select the pages you want, usually at least:
   - `Account/Login`
   - `Account/Register`
   - `Account/Logout`
5. Choose `AppDbContext` as the data context class.
6. Choose your layout page: `/Pages/Shared/_Layout.cshtml`.
7. Complete scaffolding.

### Option B: CLI scaffolder

Install the scaffolder tools if needed:

```powershell
dotnet tool install -g dotnet-aspnet-codegenerator
```

Then scaffold Identity:

```powershell
dotnet aspnet-codegenerator identity -dc RazorPageShopManager.Databases.AppDbContext --files "Account.Login;Account.Register;Account.Logout"
```

After scaffolding, the project will contain an `Areas/Identity` folder.

---

## 6. Remove or replace the current custom `Login` page

This project already has:

- `Pages/Login.cshtml`
- `Pages/Login.cshtml.cs`

That page is only a visual form right now. It is not wired to ASP.NET Core Identity because:

- the inputs are not bound with `asp-for`
- no `SignInManager` is injected
- no user validation is performed against Identity

You now have two choices.

### Choice 1: Stop using the custom page

This is the simplest and cleanest option.

Steps:

1. Use `/Identity/Account/Login` as the login page.
2. Update navigation links to point there.
3. Delete `Pages/Login.cshtml` and `Pages/Login.cshtml.cs` if they are no longer needed.

### Choice 2: Keep the custom design and wire it to Identity

If you want to keep the current custom UI, update the page model to use `SignInManager<ApplicationUser>` and bind a proper input model.

At that point, your custom page becomes a wrapper over ASP.NET Core Identity authentication.

For most projects, Choice 1 is better until the app is fully working.

---

## 7. Add login/logout links to the layout

Your `_Layout.cshtml` currently has no Identity-aware login area.

The standard approach is to create `_LoginPartial.cshtml` and render it from the layout.

Example partial:

```cshtml
@using Microsoft.AspNetCore.Identity
@using RazorPageShopManager.Entities
@inject SignInManager<ApplicationUser> SignInManager
@inject UserManager<ApplicationUser> UserManager

<ul class="navbar-nav">
@if (SignInManager.IsSignedIn(User))
{
	<li class="nav-item">
		<a class="nav-link text-dark" asp-area="Identity" asp-page="/Account/Manage/Index">
			Hello @User.Identity?.Name
		</a>
	</li>
	<li class="nav-item">
		<form class="form-inline" asp-area="Identity" asp-page="/Account/Logout" asp-route-returnUrl="@Url.Page("/Index")">
			<button type="submit" class="nav-link btn btn-link text-dark">Logout</button>
		</form>
	</li>
}
else
{
	<li class="nav-item">
		<a class="nav-link text-dark" asp-area="Identity" asp-page="/Account/Register">Register</a>
	</li>
	<li class="nav-item">
		<a class="nav-link text-dark" asp-area="Identity" asp-page="/Account/Login">Login</a>
	</li>
}
</ul>
```

Then render it in `_Layout.cshtml`.

```cshtml
<div class="navbar-collapse collapse d-sm-inline-flex justify-content-between">
    <ul class="navbar-nav flex-grow-1">
        <li class="nav-item">
            <a class="nav-link text-dark" asp-area="" asp-page="/Index">Home</a>
        </li>
        <li class="nav-item">
            <a class="nav-link text-dark" asp-area="" asp-page="/Privacy">Privacy</a>
        </li>
    </ul>

    <!-- Add below line -->
    <partial name="_LoginPartial" />
</div>
```

---

## 8. Create the Identity database migration

Once the context inherits from `IdentityDbContext<ApplicationUser>`, generate a migration.

Run these commands from the solution folder or the project folder.

```powershell
dotnet ef migrations add AddIdentityTables --project .\RazorPageShopManager
dotnet ef database update --project .\RazorPageShopManager
```

This will create the SQL Server tables for Identity in the same database referenced by:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=RazorPageShopManager;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

Expected new tables include:

- `AspNetUsers`
- `AspNetRoles`
- `AspNetUserRoles`
- `AspNetUserClaims`
- `AspNetUserLogins`
- `AspNetUserTokens`

---

## 9. Protect pages with authorization

After Identity is enabled, you can protect pages with `[Authorize]`.

Example:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorPageShopManager.Pages
{
	[Authorize]
	public class PrivacyModel : PageModel
	{
		public void OnGet()
		{
		}
	}
}
```

If you want only admins:

```csharp
[Authorize(Roles = "Admin")]
```

You can also require authorization globally:

```csharp
builder.Services.AddRazorPages(options =>
{
	options.Conventions.AuthorizeFolder("/");
	options.Conventions.AllowAnonymousToPage("/Index");
	options.Conventions.AllowAnonymousToPage("/Privacy");
});
```

That is often cleaner than adding `[Authorize]` to many individual pages.

---

## 10. Seed an admin user and roles

If this shop manager will have administration features, seed roles and a first admin account.

Typical roles:

- `Admin`
- `Manager`
- `Staff`

High-level seeding flow:

1. Create roles if they do not exist.
2. Create the admin user if it does not exist.
3. Add the admin user to the `Admin` role.

This is usually done in `Program.cs` during startup or in a separate seed class.

---

## 11. Recommended final structure

After migration, your project should look more like this:

```text
RazorPageShopManager/
  Areas/
	Identity/
	  Pages/
		Account/
  Databases/
	AppContext.cs
  Entities/
	ApplicationUser.cs
	Product.cs
  Pages/
	Shared/
	  _Layout.cshtml
	  _LoginPartial.cshtml
  Program.cs
```

---

## 12. Minimal migration checklist

If you want the shortest path, do these in order:

1. Add Identity packages.
2. Create `ApplicationUser`.
3. Change `AppDbContext` to `IdentityDbContext<ApplicationUser>`.
4. Configure `AddDefaultIdentity<ApplicationUser>()` in `Program.cs`.
5. Add `app.UseAuthentication()`.
6. Scaffold Identity pages.
7. Add login/logout UI to `_Layout.cshtml`.
8. Create migration and update the database.
9. Protect pages with `[Authorize]`.
10. Remove the old custom login page or rewire it to `SignInManager`.

---

## 13. What not to use

You asked for Identity without IdentityServer. That means:

- use ASP.NET Core Identity
- use cookie authentication
- use Razor Pages Identity UI
- do not add IdentityServer
- do not add OpenIddict unless you specifically need token issuing for external clients or APIs

For this project, plain ASP.NET Core Identity is the correct choice.

---

## 14. Suggested next implementation order for this project

For this specific codebase, the safest order is:

1. Fix package versions in `RazorPageShopManager.csproj`.
2. Add `ApplicationUser`.
3. Update `Databases/AppContext.cs`.
4. Update `Program.cs`.
5. Scaffold Identity pages into `Areas/Identity`.
6. Update `_Layout.cshtml` to include login/logout links.
7. Delete or replace the current `Pages/Login.cshtml` and `Pages/Login.cshtml.cs`.
8. Add a migration and update the database.
9. Add authorization to protected pages.

If you want, the next step after this README is to implement the migration in code directly inside the project.
