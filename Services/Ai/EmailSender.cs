using System.Net;
using System.Net.Mail;
using CleanHub.Config;
using Microsoft.Extensions.Options;

namespace CleanHub.Services.Ai
{
    /// <summary>Праќање е-пошта преку постоечките SMTP поставки (секција "SMTP").</summary>
    public class EmailSender
    {
        private readonly SMTPConfig _smtp;
        private readonly CompanyConfig _company;

        public EmailSender(IOptions<SMTPConfig> smtp, IOptions<CompanyConfig> company)
        {
            _smtp = smtp.Value;
            _company = company.Value;
        }

        public async Task SendAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default)
        {
            // Испраќач: е-поштата од Company:Email (appsettings.json); ако ја нема — SMTP:Email.
            var companyEmail = string.IsNullOrWhiteSpace(_company.Email) ? null : _company.Email.Trim();
            var from = companyEmail ?? _smtp.Email;

            try
            {
                await SendInternalAsync(from, to, subject, body, isHtml, ct);
            }
            catch (SmtpException) when (!string.Equals(from, _smtp.Email, StringComparison.OrdinalIgnoreCase))
            {
                // Некои сервери не дозволуваат праќање од адреса различна од најавената (SMTP:Email).
                // Тогаш се праќа од SMTP:Email, а одговорите одат на Company:Email.
                await SendInternalAsync(_smtp.Email, to, subject, body, isHtml, ct, replyTo: companyEmail);
            }
        }

        private async Task SendInternalAsync(string from, string to, string subject, string body, bool isHtml, CancellationToken ct, string? replyTo = null)
        {
            // Исти поставки како постоечкото праќање фактури во DocumentsController
            using var client = new SmtpClient(_smtp.Server);
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_smtp.Email, _smtp.Passwort);
            client.EnableSsl = true;

            using var message = new MailMessage
            {
                From = new MailAddress(from, _company.Name),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml,
                BodyEncoding = System.Text.Encoding.UTF8,
                SubjectEncoding = System.Text.Encoding.UTF8
            };
            message.To.Add(to);
            if (!string.IsNullOrWhiteSpace(replyTo))
                message.ReplyToList.Add(replyTo);
            CleanHub.Helpers.MailSafety.Apply(message, _smtp.TestRecipient);

            await client.SendMailAsync(message, ct);
        }
    }
}
