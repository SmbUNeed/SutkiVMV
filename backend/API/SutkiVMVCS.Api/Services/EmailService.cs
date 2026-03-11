using System.Net;
using System.Net.Mail;

namespace SutkiVMV.Services;

public interface IEmailService
{
    Task SendConfirmationEmailAsync(string toEmail, string confirmUrl);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendConfirmationEmailAsync(string toEmail, string confirmUrl)
    {
        var host     = _config["Email:Host"]!;
        var port     = int.Parse(_config["Email:Port"]!);
        var username = _config["Email:Username"]!;
        var password = _config["Email:Password"]!;
        var from     = _config["Email:From"]!;

	var client = new SmtpClient(host, port)
	{
	    EnableSsl   = false,
	    DeliveryMethod = SmtpDeliveryMethod.Network,
	    Credentials = new NetworkCredential(username, password)
	};
	client.EnableSsl = true; // STARTTLS
        var mail = new MailMessage
        {
            From       = new MailAddress(from, "SutkiVMV"),
            Subject    = "Подтверждение email — SutkiVMV",
            IsBodyHtml = true,
            Body       = $"""
                <div style="font-family:sans-serif;max-width:480px;margin:auto;">
                    <h2>Подтверждение почты</h2>
                    <p>Нажмите кнопку ниже чтобы подтвердить ваш email:</p>
                    <a href="{confirmUrl}" 
                       style="display:inline-block;padding:12px 24px;background:#2563eb;
                              color:#fff;border-radius:8px;text-decoration:none;">
                        Подтвердить email
                    </a>
                    <p style="color:#888;font-size:12px;margin-top:16px;">
                        Если вы не регистрировались — просто проигнорируйте письмо.
                    </p>
                </div>
                """
        };
        mail.To.Add(toEmail);

        await client.SendMailAsync(mail);
    }
}
