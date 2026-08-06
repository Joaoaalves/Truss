# Sending Email

`Truss.Email.Abstractions` gives the application layer one contract; `Truss.Email` gives the host the mechanisms. Handlers never know how mail leaves the system:

```csharp
public class SendWelcomeEmail(IEmailSender email) : IIntegrationEventHandler<UserRegistered>
{
    public Task Handle(UserRegistered integrationEvent, CancellationToken cancellationToken)
    {
        return email.Send(new EmailMessage(
            integrationEvent.Email,
            "Welcome",
            "<p>Your account is ready.</p>"), cancellationToken);
    }
}
```

That handler shape is the recommended one on purpose: sending from an integration event handler or a job means delivery inherits the messaging runtime's retry and dead-lettering. An email that must not be lost should never be sent inline in a command handler, where a provider hiccup would fail the request.

---

## Installing

```
truss add email                     # console provider
truss add email --provider smtp     # smtp provider with Mailpit for development
```

The command references `Truss.Email.Abstractions` in the application layer, `Truss.Email` in the hosts (the worker included, when present) and registers the sender:

| Provider | Behavior |
|---|---|
| `console` | Messages print to the log; reset links and codes show up right in the terminal |
| `smtp` | Delivery over SMTP through MailKit; development points at Mailpit |

With docker and the smtp provider, `docker compose up` starts [Mailpit](https://mailpit.axllent.org), a local SMTP server with a web inbox at `http://localhost:8025`; `truss dev` prints the URL. Every email the application sends lands there, visible and clickable, with nothing leaving the machine.

---

## Configuration

The SMTP sender binds from the `Truss:Email:Smtp` section, which the scaffold writes for development; production overrides per environment:

```
Truss__Email__Smtp__Host=smtp.example.com
Truss__Email__Smtp__Port=587
Truss__Email__Smtp__UserName=apikey
Truss__Email__Smtp__Password=<secret>
Truss__Email__Smtp__From=noreply@example.com
Truss__Email__Smtp__UseStartTls=true
```

Any SMTP provider works, which keeps the free-first principle: a transactional provider's SMTP endpoint, a self-hosted relay, or the company server. Keep the password out of source control; bind it from the environment.

`EmailMessage` carries the recipient, the subject and an HTML body with an optional plain text alternative. A custom `IEmailSender` implementation swaps the mechanism (a provider API, a fake for tests) without touching a handler.
