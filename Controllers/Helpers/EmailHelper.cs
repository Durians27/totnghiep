using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace VelvySkinWeb.Helpers
{
    public static class EmailHelper
    {
        public static async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            string myGmail = "cskh.velvyskin@gmail.com"; 
            string myAppPassword = "dngv tavx omxe dmmc"; 

            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(myGmail, myAppPassword),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(myGmail, "Velvy Skin CSKH"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true, 
            };
            
            mailMessage.To.Add(toEmail);
            await smtpClient.SendMailAsync(mailMessage);
        }
    }
}