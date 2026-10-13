using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace SutkiVMVCS.Services.Shared
{
    public class FcmService
    {
        public FcmService(IConfiguration config)
        {
            FirebaseApp.Create(new AppOptions
            {
#pragma warning disable
                Credential = GoogleCredential
                    .FromFile(config["Firebase:ServiceAccountPath"]!)
                    .CreateScoped("https://www.googleapis.com/auth/firebase.messaging")
            });
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
