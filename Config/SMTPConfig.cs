using System.ComponentModel.DataAnnotations;

namespace CleanHub.Config
{
    public class SMTPConfig
    {
        public string Server { get; set; }
        public int Port { get; set; }
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }
        [DataType(DataType.EmailAddress)]
        public string Recipient { get; set; }
        public string Passwort { get; set; }
    }
}
