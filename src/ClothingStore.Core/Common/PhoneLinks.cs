using System.Text;

namespace ClothingStore.Core;

/// <summary>Turns phone numbers typed any old way into WhatsApp links.</summary>
public static class PhoneLinks
{
    /// <summary>
    /// International digits for wa.me, e.g. "03 123 456" -> "9613123456", "+961 71 234 567" -> "96171234567".
    /// Local numbers (leading 0, or 7-8 digits with no country code) get <paramref name="countryCode"/>. Null if no digits.
    /// </summary>
    public static string? WhatsAppNumber(string? phone, string countryCode = "961")
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0) return null;

        var international = phone.TrimStart().StartsWith('+');
        if (digits.StartsWith("00")) { digits = digits[2..]; international = true; }
        if (international || digits.StartsWith(countryCode) && digits.Length > countryCode.Length + 6) return digits;
        if (digits.StartsWith('0')) digits = digits[1..];
        return countryCode + digits;
    }

    /// <summary>https://wa.me/{number}?text=... (opens WhatsApp / WhatsApp Web with the message ready to send).</summary>
    public static string? WhatsAppLink(string? phone, string message, string countryCode = "961")
    {
        var number = WhatsAppNumber(phone, countryCode);
        if (number is null) return null;
        var link = new StringBuilder("https://wa.me/").Append(number);
        if (!string.IsNullOrEmpty(message)) link.Append("?text=").Append(Uri.EscapeDataString(message));
        return link.ToString();
    }
}
