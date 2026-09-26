using Microsoft.AspNetCore.DataProtection;

namespace SutkiVMVCS.Api.Services.Security;

public class FieldEncryptor
{
    private readonly IDataProtector _protector;

    public FieldEncryptor(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("Client.PII.v1");

    public string Encrypt(string value) => string.IsNullOrEmpty(value) ? value : _protector.Protect(value);
    public string Decrypt(string value) => string.IsNullOrEmpty(value) ? value : _protector.Unprotect(value);
}