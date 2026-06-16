using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace SutkiVMVCS.Services
{
    public class FcmService
    {
        public FcmService(IConfiguration config)
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromFile(
                        config["Firebase:ServiceAccountPath"]!
                    )
                });
            }
        }

        public async Task SendAsync(string token, string title, string body,
        Dictionary<string, string>? data = null)
        {
            var message = new Message
            {
                Token = token,
                Notification = new Notification { Title = title, Body = body },
                Data = data ?? new Dictionary<string, string>()
            };

            await FirebaseMessaging.DefaultInstance.SendAsync(message);
        }
    }
}
