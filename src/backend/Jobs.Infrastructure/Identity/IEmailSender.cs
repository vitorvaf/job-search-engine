using System.Net;
using System.Net.Mail;
using Jobs.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jobs.Infrastructure.Identity;

public sealed record EmailMessage(
    string ToAddress,
    string Subject,
    string TextBody,
    string? HtmlBody = null,
    string? ToName = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IOptions<AppOptions> _appOptions;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<AppOptions> appOptions, ILogger<SmtpEmailSender> logger)
    {
        _appOptions = appOptions;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var emailOptions = _appOptions.Value.Email;
        var smtp = emailOptions.Smtp;

        using var mail = new MailMessage
        {
            From = new MailAddress(emailOptions.FromAddress, emailOptions.FromName),
            Subject = message.Subject,
            Body = string.IsNullOrWhiteSpace(message.HtmlBody) ? message.TextBody : message.HtmlBody,
            IsBodyHtml = !string.IsNullOrWhiteSpace(message.HtmlBody)
        };

        mail.To.Add(string.IsNullOrWhiteSpace(message.ToName)
            ? new MailAddress(message.ToAddress)
            : new MailAddress(message.ToAddress, message.ToName));

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(smtp.Username))
        {
            client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);
        }

        await client.SendMailAsync(mail);

        _logger.LogInformation("Email sent: to={To} subject={Subject}", message.ToAddress, message.Subject);
    }
}
