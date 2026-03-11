using Resend;

namespace SutkiVMV.Services;

public interface IEmailService
{
    Task SendConfirmationEmailAsync(string toEmail, string confirmUrl);
}

public class EmailService : IEmailService
{
    private readonly IResend _resend;

    public EmailService(IResend resend)
    {
        _resend = resend;
    }

    public async Task SendConfirmationEmailAsync(string toEmail, string confirmUrl)
    {
        var message = new EmailMessage
        {
            From = "SutkiVMV <no-reply@sutkivmv.ru>",
            To = { toEmail },
            Subject = "Подтверждение email — SutkiVMV",
            HtmlBody = $"""
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

        await _resend.EmailSendAsync(message);
    }
}