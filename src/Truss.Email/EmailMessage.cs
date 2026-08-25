namespace Truss.Email
{
    /// <summary>
    /// An email to send: the recipient, the subject and the body in HTML with an
    /// optional plain text alternative.
    /// </summary>
    /// <param name="To">The recipient address.</param>
    /// <param name="Subject">The subject line.</param>
    /// <param name="HtmlBody">The HTML body.</param>
    /// <param name="TextBody">The plain text alternative, when any.</param>
    public sealed record EmailMessage(string To, string Subject, string HtmlBody, string? TextBody = null);
}
