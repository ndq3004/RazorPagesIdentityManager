# Two-Factor Authentication (TOTP) Flow

By default, ASP.NET Core Identity signs users in immediately after a correct password. The redirect to a TOTP verification step only happens when `TwoFactorEnabled = true` on the user's account. This guide explains how the flow works and how to enable it.

---

## How the flow works

```
Browser                    Login.cshtml          SignInManager         AspNetUsers / Tokens
   |                           |                       |                      |
   |-- POST /Login ----------->|                       |                      |
   |                           |-- PasswordSignInAsync -->                    |
   |                           |                       |-- check password --->|
   |                           |                       |<-- valid ------------|
   |                           |                       |-- RequiresTwoFactor? yes
   |                           |                       |-- set Identity.TwoFactorUserId cookie
   |                           |<-- RequiresTwoFactor --|
   |<-- 302 /LoginWith2fa -----|                       |                      |
   |                           |                       |                      |
   |-- POST /LoginWith2fa ---->|                       |                      |
   |  (TOTP code submitted)    |-- TwoFactorAuthenticatorSignInAsync -->       |
   |                           |                       |-- validate TOTP ---->|
   |                           |                       |<-- valid ------------|
   |                           |                       |-- issue auth cookie  |
   |                           |                       |-- clear temp cookie  |
   |<-- 302 ReturnUrl ---------|                       |                      |
```

What triggers the redirect in `Login.cshtml.cs`:

```csharp
var result = await _signInManager.PasswordSignInAsync(
    Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

if (result.RequiresTwoFactor)
{
    return RedirectToPage("./LoginWith2fa", new
    {
        ReturnUrl = returnUrl,
        RememberMe = Input.RememberMe
    });
}
```

`result.RequiresTwoFactor` is `true` only when the password was correct AND the user has `TwoFactorEnabled = true` in `AspNetUsers`. No code changes to this file are needed — the scaffolded version already contains this redirect.

The `Identity.TwoFactorUserId` temporary cookie:

- Short-lived, expires with the browser session.
- Not a full auth cookie — only identifies who is mid-login.
- Automatically cleared after successful 2FA or on logout.
- If it expires before the TOTP code is submitted, the user must start login again.

---

## Step 1: Confirm authenticator setup pages are scaffolded

Check if this file exists:

```
Areas/Identity/Pages/Account/Manage/EnableAuthenticator.cshtml
```

If it does not exist, scaffold it:

```powershell
dotnet aspnet-codegenerator identity -dc RazorPageShopManager.Databases.AppDbContext --files "Account.Manage.EnableAuthenticator;Account.Manage.TwoFactorAuthentication"
```

---

## Step 2: Add QR code script to EnableAuthenticator page

Open `Areas/Identity/Pages/Account/Manage/EnableAuthenticator.cshtml`.

Find the `@section Scripts` block at the bottom and add the QR code library:

```html
@section Scripts {
    <script src="https://cdnjs.cloudflare.com/ajax/libs/qrcodejs/1.0.0/qrcode.min.js"></script>
    <script>
        new QRCode(document.getElementById("qrCode"), {
            text: document.getElementById("qrCodeData").getAttribute("data-url"),
            width: 200,
            height: 200
        });
    </script>
}
```

The scaffolded page already has `<div id="qrCode">` and `<div id="qrCodeData" data-url="...">` — the script above wires them together.

---

## Step 3: Add a link to the manage page

If there is no link to the account management area, add one to `_LoginPartial.cshtml` or the navbar:

```cshtml
<a asp-area="Identity" asp-page="/Account/Manage/Index">Manage Account</a>
```

Or navigate directly in the browser while logged in:

```
/Identity/Account/Manage/TwoFactorAuthentication
```

---

## Step 4: Enable 2FA on the user account

1. Run the app and log in.
2. Navigate to `/Identity/Account/Manage/TwoFactorAuthentication`.
3. Click **Add authenticator app**.
4. A QR code and a manual key are shown.
5. Open Google Authenticator or Microsoft Authenticator on your phone.
6. Scan the QR code (or enter the manual key).
7. Enter the 6-digit code shown in the app into the verification field on the page.
8. Click **Verify**.

After this, `TwoFactorEnabled = 1` is written to the user's row in `AspNetUsers`.

---

## Step 5: Verify the flow

1. Log out.
2. Go to the login page and enter email + password.
3. Identity detects `TwoFactorEnabled = true` and redirects to `/Identity/Account/LoginWith2fa`.
4. Enter the 6-digit code from your authenticator app.
5. You are signed in.

---

## RequireConfirmedAccount interaction

Your current `Program.cs` has:

```csharp
options.SignIn.RequireConfirmedAccount = true;
```

Email confirmation and 2FA are independent:

- `RequireConfirmedAccount` blocks login until the user confirms their email address.
- 2FA adds a second factor after the password step, regardless of email confirmation status.
- If `RequireConfirmedAccount = true` and the email is not confirmed, the user never reaches the 2FA step because they are blocked earlier.

During development, set `RequireConfirmedAccount = false` to avoid being blocked by email confirmation while testing the 2FA flow.

---

## Recovery codes

After enabling 2FA, Identity generates one-time-use recovery codes shown on the `ShowRecoveryCodes` page.

- Used when the user loses access to their authenticator device.
- Each code can only be used once.
- Cannot be retrieved again after the initial display.
- New codes can be generated from `/Identity/Account/Manage/TwoFactorAuthentication`.

Always save these codes somewhere safe when setting up 2FA.

---

## No extra packages required

TOTP 2FA is fully built into ASP.NET Core Identity. The `Microsoft.AspNetCore.Identity.UI` package you already have covers:

- `LoginWith2fa` page
- `EnableAuthenticator` page
- `TwoFactorAuthentication` manage page
- `ShowRecoveryCodes` page
- `Disable2fa` page
- `ResetAuthenticatorKey` page
