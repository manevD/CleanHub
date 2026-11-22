using System.Net;
using System.Net.Mail;
using CleanHub.Config;
using Microsoft.Extensions.Options;

namespace CleanHub.Services.Ai
{
    /// <summary>
    /// Праќање е-пошта — истата логика како „Зачувај, прати и принтај“ (DocumentsController.CreateAndSend):
    /// SmtpClient(SMTP:Server), најава SMTP:Email / SMTP:Passwort, SSL, од SMTP:Email.
    /// </summary>
    public class EmailSender
    {
        private readonly SMTPConfig _smtp;

        public EmailSender(IOptions<SMTPConfig> smtp)
        {
            _smtp = smtp.Value;
        }

        public async Task SendAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default)
        {
            using (SmtpClient smtpClient = new SmtpClient(_smtp.Server))
            {
                smtpClient.Credentials = new NetworkCredential(_smtp.Email, _smtp.Passwort);
                smtpClient.EnableSsl = true;

                MailMessage message = new MailMessage();
                message.From = new MailAddress(_smtp.Email);
                message.To.Add(to);
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = isHtml;
                smtpClient.UseDefaultCredentials = false;
                await smtpClient.SendMailAsync(message);
            }
        }
    }
}
