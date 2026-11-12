using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace SutkiVMVCS.Services.Shared
{
    public class FcmService
    {
        public FcmService(IConfiguration config)
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                var path = config["Firebase:ServiceAccountPath"]
                    ?? throw new InvalidOperationException("Firebase:ServiceAccountPath is not configured.");

                FirebaseApp.Create(new AppOptions
                {
                    Credential = CredentialFactory
                        .FromFile<ServiceAccountCredential>(path)
                        .ToGoogleCredential()
                        .CreateScoped("https://www.googleapis.com/auth/firebase.messaging")
                });
            }
        }

        public async Task<bool> SendAsync(string token, string title, string body,
        Dictionary<string, string>? data = null)
        {
            var message = new Message
            {
                Token = token,
                Notification = new Notification { Title = title, Body = body },
                Data = data ?? new Dictionary<string, string>()
            };

            try
            {
                await FirebaseMessaging.DefaultInstance.SendAsync(message);
                return true;
            }
            catch (FirebaseMessagingException e)
            {
                if (e.MessagingErrorCode == MessagingErrorCode.Unregistered)
                {
                    return false;
                }

                Console.WriteLine($"Ошибка FCM (не Unregistered): {e.Message}");
                throw;
            }
        }
    }
}
