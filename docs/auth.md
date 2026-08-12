# Authentication

Truss authentication splits responsibility deliberately: the packages own the mechanics that should never be hand-rolled, and **your project owns the model**. Installing auth scaffolds the `User` aggregate, the account commands and the repositories into your own layers as plain, editable code. Need a phone number on registration, a tenant id on the user, a different lockout policy? Edit your files; no framework type stands in the way.

---

## Installing

```
truss add auth --provider jwt
```

Requires a database. The command:

- References `Truss.Auth.Abstractions` in the application layer and `Truss.Auth.Jwt` in the host.
- Scaffolds the `Accounts` context, laid out exactly like [generated code](cli.md#truss-generate): namespaces mirror the folders, the aggregate owns its value objects, events and rules, and each command owns a folder with its handler and validator.
- Wires `Program.cs`: `AddTrussJwtAuth`, `UseAuthentication`, `UseAuthorization` and the three endpoints.
- Writes a development signing key to `appsettings.json`.

```
Domain/Accounts/User/
  User.cs                       the aggregate, namespace MyShop.Domain.Accounts.User
  ValueObjects/UserId.cs
  Events/UserRegistered.cs
  Rules/EmailMustBeUnique.cs

Application/Accounts/
  IUserRepository.cs            the repository and the credential and token stores
  IUserCredentialsStore.cs
  IRefreshTokenStore.cs
  DTOs/AuthTokensDto.cs
  Rules/InvalidCredentials.cs
  RegisterUser/                 command, handler and validator
  Login/                        command, handler and validator
  Refresh/                      command, handler and validator

Infrastructure/Accounts/        persistence models, EF configurations and store implementations
```

The account flows and external login add their own folders beside these (`ConfirmEmail/`, `ResetPassword/`, `ExternalLogin/`, and so on).

The endpoints work immediately:

```
POST /auth/register  {"email": "joao@example.com", "name": "Joao", "password": "..."}
POST /auth/login     {"email": "joao@example.com", "password": "..."}      -> access + refresh tokens
POST /auth/refresh   {"refreshToken": "..."}                               -> rotated tokens
```

With the [email module](email.md) installed before auth, the scaffold also carries the account flows: password reset, email confirmation and two factor login by email.

If auth came first, the flows are not lost. Install email and retrofit them:

```bash
truss add email --provider smtp
truss add auth --flows
```

The retrofit writes the flow slices, registers their stores, swaps `Login` and `RegisterUser` for the flow-aware variants and rewires the auth routes. The account slice is your code, so any file you edited since scaffolding is left alone and listed for you to port by hand; `Login` and its handler change contract together (`AuthTokensDto` becomes `LoginResult`), so that pair is only swapped when both are untouched, and the login route keeps its old contract otherwise.

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

## Binding the User to Your Aggregate

Most systems already have the person somewhere in the domain: a `Customer`, a `Member`, a `Doctor`. `--bind-user` connects the account to that aggregate at scaffold time, in whichever of the two shapes fits your model:

```
truss add auth --bind-user Customer                      # reference (default)
truss add auth --bind-user Customer --bind-mode merge    # the aggregate is the account
```

**Reference mode** keeps identity and business in separate contexts, which is the cleanest DDD cut: the scaffolded `User` carries the aggregate's typed id, `RegisterUser` takes it (`{"email": ..., "name": ..., "password": ..., "customerId": ...}`), and every issued token carries a `customerId` claim, so handlers can reach the business aggregate without a lookup. Create the `Customer` through your own commands first, then register the login for it.

**Merge mode** says your aggregate IS the account: no `User` is scaffolded at all. The commands and stores are generated against `User` and `UserId` as usual, and a pair of `global using` aliases (`AccountAliases.cs`) points those names at your aggregate and its typed id, so all the scaffolded code operates on it directly and stays fully editable. The aggregate must own the identity fields; if it does not, the CLI prints exactly what to add:

```csharp
public string Email { get; private set; } = string.Empty;
public string Name { get; private set; } = string.Empty;
public static Customer Register(string email, string name) { ... }
```

Credentials, refresh tokens and account flows stay in infrastructure behind the same stores in both modes; binding never moves authentication state into the domain. If the merged aggregate has no EF configuration yet, one is generated mapping `Email` uniquely.

Choose reference when the person and the account can diverge (one account operating several business entities, accounts created before the business relationship). Choose merge when a separate `User` would only mirror your aggregate one to one.

---

## External Login Providers

```
truss add auth --external google,microsoft,github
```

Combinable with either credential provider and with the binding, at install time or later. Each provider is wired as an OAuth scheme over the standard ASP.NET Core handlers (Google and Microsoft from Microsoft's packages, GitHub from the community `AspNet.Security.OAuth.GitHub`), and the flow ends in your own code:

```
GET /auth/external/google            -> redirects to the provider
GET /auth/external/google/callback   -> access + refresh tokens, same shape as /auth/login
```

The callback dispatches a scaffolded `ExternalLogin` command. Its handler resolves the account: a login already linked in the `ExternalLogins` table wins; otherwise the account registered for the provider's email is linked; otherwise a new account is provisioned from the external profile (name and email, no password). Social login and password coexist on the same account, and the handler is your code, so the policy is yours to change.

With a reference binding the last step differs: an account cannot be provisioned from an external profile alone, because it must reference your aggregate. Unknown emails are rejected with the stable code `accounts.external-unlinked`; register first, then sign in externally.

Configuration lives under `Truss:Auth:External`; the id can sit in `appsettings.json`, the secret comes from the environment:

```
Truss__Auth__External__Google__ClientId=...
Truss__Auth__External__Google__ClientSecret=...
```

Register the callback in each provider console as `https://<host>/signin-google`, `/signin-microsoft` or `/signin-github`. Two factor is not asked for external logins: the provider already authenticated the person.

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

Further external providers land by demand. The scaffolded model is provider-independent: switching providers later means changing the mechanics, not your domain.
