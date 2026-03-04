namespace ERP.BLL.Interfaces
{
    /// <summary>
    /// Simple email sending abstraction.
    /// Implementations should be resilient — failures logged, never thrown.
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Send a plain-text email asynchronously.
        /// Returns true if sent successfully, false otherwise.
        /// </summary>
        Task<bool> SendAsync(string toEmail, string subject, string body);

        /// <summary>
        /// Send an HTML email asynchronously.
        /// Returns true if sent successfully, false otherwise.
        /// </summary>
        Task<bool> SendHtmlAsync(string toEmail, string subject, string htmlBody);
    }
}
