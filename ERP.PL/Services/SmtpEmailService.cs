using ERP.BLL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace ERP.PL.Services
{
    /// <summary>
    /// SMTP-based email service. Reads configuration from appsettings "Email" section.
    /// If SMTP is not configured, all sends are logged and return false (no-op mode).
    /// This ensures the system runs cleanly in development without SMTP setup.
    /// </summary>
    public sealed class SmtpEmailService : IEmailService
    {
        private readonly ILogger<SmtpEmailService> _logger;
        private readonly string? _host;
        private readonly int _port;
        private readonly string? _username;
        private readonly string? _password;
        private readonly string _fromEmail;
        private readonly string _fromName;
        private readonly bool _enableSsl;
        private readonly bool _isConfigured;

        public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
        {
            _logger = logger;

            var section = configuration.GetSection("Email");
            _host = section["SmtpHost"];
            _port = section.GetValue("SmtpPort", 587);
            _username = section["SmtpUsername"];
            _password = section["SmtpPassword"];
            _fromEmail = section["FromEmail"] ?? "noreply@companyflow.local";
            _fromName = section["FromName"] ?? "CompanyFlow ERP";
            _enableSsl = section.GetValue("EnableSsl", true);

            _isConfigured = !string.IsNullOrWhiteSpace(_host);

            if (!_isConfigured)
            {
                _logger.LogInformation("SmtpEmailService: SMTP not configured (Email:SmtpHost is empty). " +
                                       "Email sends will be logged but not delivered.");
            }
        }

        public async Task<bool> SendAsync(string toEmail, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                return false;

            if (!_isConfigured)
            {
                _logger.LogInformation(
                    "SmtpEmailService [NO-OP]: Would send email to {To}, Subject: {Subject}",
                    toEmail, subject);
                return false;
            }

            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(_fromEmail, _fromName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };
                message.To.Add(toEmail);

                using var client = new SmtpClient(_host!, _port)
                {
                    EnableSsl = _enableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                if (!string.IsNullOrWhiteSpace(_username))
                {
                    client.Credentials = new NetworkCredential(_username, _password);
                }

                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent successfully to {To}: {Subject}", toEmail, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send email to {To}: {Subject}", toEmail, subject);
                return false;
            }
        }
    }
}
