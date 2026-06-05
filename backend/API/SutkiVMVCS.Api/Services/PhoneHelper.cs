namespace SutkiVMVCS.Services
{
    public static class PhoneHelper
    {
        public static string? Normalize(string? phone)
        {
            if (string.IsNullOrEmpty(phone)) return null;

            var digits = new string(phone.Where(char.IsDigit).ToArray());

            if (digits.Length == 11 && digits.StartsWith("8"))
                digits = "7" + digits[1..];

            if (digits.Length == 10)
                digits = "7" + digits;

            if (digits.Length != 11 || !digits.StartsWith("7"))
                return phone.Trim();

            return "+" + digits;
        }
    }
}
