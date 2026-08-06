# Authentication

Truss authentication splits responsibility deliberately: the packages own the mechanics that should never be hand-rolled, and **your project owns the model**. Installing auth scaffolds the `User` aggregate, the account commands and the repositories into your own layers as plain, editable code. Need a phone number on registration, a tenant id on the user, a different lockout policy? Edit your files; no framework type stands in the way.

---

## Installing

```
truss add auth --provider jwt
```

Requires a database. The command:

- References `Truss.Auth.Abstractions` in the application layer and `Truss.Auth.Jwt` in the host.
- Scaffolds the `Accounts` context: the `User` aggregate, its business rule and the `UserRegistered` event in the domain; the `RegisterUser`, `Login` and `Refresh` commands with handlers, validators and the credential store abstractions in the application; the credential and refresh token persistence models, EF configurations and store implementations in the infrastructure.
- Wires `Program.cs`: `AddTrussJwtAuth`, `UseAuthentication`, `UseAuthorization` and the three endpoints.
- Writes a development signing key to `appsettings.json`.

The endpoints work immediately:

```
POST /auth/register  {"email": "joao@example.com", "name": "Joao", "password": "..."}
POST /auth/login     {"email": "joao@example.com", "password": "..."}      -> access + refresh tokens
POST /auth/refresh   {"refreshToken": "..."}                               -> rotated tokens
```

With the [email module](email.md) installed before auth, the scaffold also carries the account flows: password reset, email confirmation and two factor login by email. Install email first to get them.

---

## What the Package Owns

`Truss.Auth.Jwt` carries the security mechanics:

- **Password hashing**: PBKDF2 with SHA-256, 210000 iterations, per-password random salt, self-describing hash format and constant-time verification. Base class library only.
- **Access tokens**: HMAC-SHA256 signed JWTs carrying the claims you choose, with configurable issuer, audience and lifetime.
- **Refresh tokens**: opaque random tokens. Only the SHA-256 hash is stored, so a database leak exposes nothing usable. The scaffolded `Refresh` handler rotates tokens: each refresh revokes the old one and issues a new pair, atomically with the unit of work.
- **JwtBearer wiring**: token validation configured from the options, with inbound claim mapping disabled so claims arrive exactly as issued (`sub`, `email`).

Configuration binds from the `Truss:Auth:Jwt` section or environment variables:

```
Truss__Auth__Jwt__SigningKey=<at least 32 characters>
Truss__Auth__Jwt__Issuer=MyShop
Truss__Auth__Jwt__Audience=MyShop
Truss__Auth__Jwt__AccessTokenLifetime=00:15:00
```

The scaffolded development key must be replaced per environment; keep the production key out of source control.

---

## What Your Project Owns

Everything about the model. The scaffolded `User` is a normal Truss aggregate, and it carries **no authentication state**: no password hash, no tokens. Credentials are not a domain concern.

```csharp
public class User : AggregateRoot<UserId>
{
    public string Email { get; private set; }
    public string Name { get; private set; }

    public static User Register(string email, string name) { ... }
}
```

Password hashes and refresh tokens live in the infrastructure layer as persistence models (`UserCredential`, `RefreshTokenRecord`), reached through two application abstractions the scaffold also generates:

```csharp
public interface IUserCredentialsStore
{
    Task SetPassword(UserId userId, string password, CancellationToken cancellationToken = default);
    Task<bool> VerifyPassword(UserId userId, string password, CancellationToken cancellationToken = default);
}
```

The abstraction takes plain passwords and answers yes or no; hashing happens inside the store implementation, so no hash ever crosses the application layer and the provider can be swapped without touching a handler.

Add fields, rules and events to the aggregate like in any other; the EF configurations sit next to your others. The handlers are ordinary command handlers composing the domain, the stores and the package services:

```csharp
public class LoginHandler(
    IUserRepository users,
    IUserCredentialsStore credentials,
    IRefreshTokenStore refreshTokens,
    IJwtTokenService tokens) : ICommandHandler<Login, AuthTokensDto>
```

Invalid credentials surface as a business rule violation, which the ASP.NET module turns into a clean 422 response; change that behavior in your own handler if you prefer another status.

---

## Protecting Endpoints

Every Truss mapping composes with the standard authorization surface:

```csharp
app.MapCommand<PlaceOrder, Guid>("/orders").RequireAuthorization();
```

Handlers read the current identity through `ClaimsPrincipal` in endpoints, or flow it into commands explicitly; the `sub` claim carries the user id issued at login.

---

## The Identity Provider

```
truss add auth --provider identity
```

Same endpoints, same domain, same JWT issuance; the credential mechanics run through ASP.NET Core Identity instead of the Truss hasher. The scaffold generates an `ApplicationUser` (an `IdentityUser<Guid>` keyed by the same id as your `User` aggregate), the EF configurations for the Identity tables and an `IUserCredentialsStore` backed by `UserManager`, wired with `AddIdentityCore` in the scaffolded `AccountsModule`.

Choose it when you want Identity's ecosystem: its password hasher and upgrade path, password policies (tuned in `AccountsModule.cs`, aligned by default with the scaffolded validator), and a direct road to lockout, password reset and external login providers via `UserManager`. The domain `User` stays exactly as clean as with the JWT provider; only infrastructure changes.

---

## Account Flows

When the email module is present at `truss add auth` time, four more endpoints come along, built on single-use tokens that are stored only as hashes, expire, and are consumed atomically with the command that uses them:

```
POST /auth/password/request-reset  {"email": "..."}                  -> always 204; a reset token arrives by email
POST /auth/password/reset          {"token": "...", "newPassword": "..."}
POST /auth/confirm-email           {"token": "..."}
POST /auth/login/2fa               {"email": "...", "code": "123456"} -> access + refresh tokens
```

`Login` then returns a `LoginResult`: with two factor off, the tokens directly; with it on, `requiresTwoFactor: true` and a six digit code by email, verified at `/auth/login/2fa`. The reset request answers 204 for unknown addresses too, so the endpoint never confirms whether an account exists, and registration validates the address with the [deliverability validator](email.md) before anything is stored.

Account security state stays out of the domain, by the same rule as the credentials: `EmailConfirmed` and `TwoFactorEnabled` live on the infrastructure account model (the `UserCredential` for the JWT provider; the `IdentityUser`, which carries both natively, for Identity) behind the scaffolded `IAccountSecurityStore`. Reading, confirming and toggling go through it:

```csharp
public interface IAccountSecurityStore
{
    Task<AccountSecurity> Get(UserId userId, CancellationToken cancellationToken = default);
    Task ConfirmEmail(UserId userId, CancellationToken cancellationToken = default);
    Task SetTwoFactorEnabled(UserId userId, bool enabled, CancellationToken cancellationToken = default);
}
```

Exposing a "turn on two factor" surface is your call: an authenticated endpoint calling `SetTwoFactorEnabled` with the current user's id from the `sub` claim is a handful of lines in your own code. Every email the flows send goes through `IEmailSender`, so in development they land in the console log or the Mailpit inbox.

---

## Roadmap

External OpenID providers (Google, Microsoft, GitHub) are planned. The scaffolded model is provider-independent: switching providers later means changing the mechanics, not your domain.
