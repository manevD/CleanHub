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
            // Исти поставки како постоечкото праќање фактури во DocumentsController
            using var client = new SmtpClient(_smtp.Server);
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_smtp.Email, _smtp.Passwort);
            client.EnableSsl = true;

            using var message = new MailMessage
            {
                From = new MailAddress(_smtp.Email, _company.Name),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml,
                BodyEncoding = System.Text.Encoding.UTF8,
                SubjectEncoding = System.Text.Encoding.UTF8
            };
            message.To.Add(to);
            CleanHub.Helpers.MailSafety.Apply(message, _smtp.TestRecipient);

            await client.SendMailAsync(message, ct);
        }
    }
}
