namespace SutkiVMVCS.Services.Security
{
    public class TrigramService
    {
        private readonly byte[] _hmacKey;
        public TrigramService(byte[] hmacKey) {
            _hmacKey = hmacKey;
        }
    }
}
