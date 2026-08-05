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
    Task SetPassword(UserId userId, string passwordHash, CancellationToken cancellationToken = default);
    Task<string?> GetPasswordHash(UserId userId, CancellationToken cancellationToken = default);
}
```

Add fields, rules and events to the aggregate like in any other; the EF configurations sit next to your others. The handlers are ordinary command handlers composing the domain, the stores and the package services:

```csharp
public class LoginHandler(
    IUserRepository users,
    IUserCredentialsStore credentials,
    IRefreshTokenStore refreshTokens,
    IPasswordHasher passwordHasher,
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

## Roadmap

ASP.NET Core Identity integration (`--provider identity`) and external OpenID providers are planned. The scaffolded model is provider-independent: switching providers later means changing the mechanics, not your domain.
