using System.Net.Mail;

namespace CleanHub.Helpers
{
    /// <summary>
    /// Тест-режим за е-пошта. Ако е поставено SMTP:TestRecipient, СЕКОЈА порака од апликацијата
    /// оди само на таа адреса (наместо до станарите), а насловот покажува до кого требало да оди.
    /// </summary>
    public static class MailSafety
    {
        public static bool IsTestMode(string? testRecipient) => !string.IsNullOrWhiteSpace(testRecipient);

        public static void Apply(MailMessage message, string? testRecipient)
        {
            if (!IsTestMode(testRecipient)) return;

            var original = string.Join(", ",
                message.To.Concat(message.CC).Concat(message.Bcc).Select(a => a.Address));

            message.To.Clear();
            message.CC.Clear();
            message.Bcc.Clear();

            foreach (var address in testRecipient!.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                message.To.Add(address);

            message.Subject = $"[ТЕСТ → {(original.Length > 0 ? original : "нема адреса")}] {message.Subject}";
        }
    }
}
