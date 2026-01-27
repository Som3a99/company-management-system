using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace ERP.PL.Helpers
{
    /// <summary>
    /// Input sanitization service to prevent XSS and injection attacks
    /// </summary>
    public class InputSanitizer
    {
        /// <summary>
        /// Sanitize HTML input - encode dangerous characters
        /// </summary>
        public static string SanitizeHtml(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Encode HTML special characters
            return HttpUtility.HtmlEncode(input);
        }

        /// <summary>
        /// Sanitize string for SQL LIKE queries
        /// </summary>
        public static string SanitizeLikeQuery(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Escape SQL LIKE special characters
            return input
                .Replace("[", "[[]")
                .Replace("%", "[%]")
                .Replace("_", "[_]");
        }

        /// <summary>
        /// Remove potentially dangerous characters from input
        /// </summary>
        public static string RemoveDangerousCharacters(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Remove script tags and other dangerous HTML
            var cleaned = Regex.Replace(input, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"<iframe[^>]*>[\s\S]*?</iframe>", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"javascript:", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"on\w+\s*=", "", RegexOptions.IgnoreCase);

            return cleaned;
        }

        /// <summary>
        /// Sanitize filename - remove path traversal attempts
        /// </summary>
        public static string SanitizeFilename(string? filename)
        {
            if (string.IsNullOrEmpty(filename))
                return string.Empty;

            // Remove path separators
            var cleaned = filename.Replace("../", "").Replace("..\\", "");
            cleaned = cleaned.Replace("/", "").Replace("\\", "");
            cleaned = cleaned.Replace(":", "");

            // Remove other dangerous characters
            var invalidChars = Path.GetInvalidFileNameChars();
            cleaned = string.Join("", cleaned.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            return cleaned;
        }

        /// <summary>
        /// Validate and sanitize email address
        /// </summary>
        public static string? SanitizeEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            email = email.Trim().ToLowerInvariant();

            // Basic email format validation
            if (!Regex.IsMatch(email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
                return null;

            return email;
        }

        /// <summary>
        /// Sanitizes a phone number by removing non-digit characters and formatting
        /// </summary>
        /// <param name="phoneNumber">Raw phone number input</param>
        /// <returns>Sanitized phone number or null if invalid</returns>
        public static string? SanitizePhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return null;

            // Remove all non-digit characters except leading +
            var digits = new StringBuilder();
            bool hasLeadingPlus = false;

            for (int i = 0; i < phoneNumber.Length; i++)
            {
                char c = phoneNumber[i];

                if (i == 0 && c == '+')
                {
                    hasLeadingPlus = true;
                    digits.Append(c);
                }
                else if (char.IsDigit(c))
                {
                    digits.Append(c);
                }
            }

            var result = digits.ToString();

            // Basic validation
            if (hasLeadingPlus)
            {
                // International format: + followed by 1-15 digits
                if (result.Length < 2 || result.Length > 16) // + plus 1-15 digits
                    return null;
            }
            else
            {
                // Local format: 7-15 digits
                if (result.Length < 7 || result.Length > 15)
                    return null;
            }

            return result;
        }

        /// <summary>
        /// Sanitize alphanumeric input
        /// </summary>
        public static string SanitizeAlphanumeric(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Keep only letters, numbers, and spaces
            return Regex.Replace(input, @"[^a-zA-Z0-9\s]", "");
        }

        /// <summary>
        /// Trim and normalize whitespace
        /// </summary>
        public static string NormalizeWhitespace(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Replace multiple spaces with single space
            var normalized = Regex.Replace(input.Trim(), @"\s+", " ");
            return normalized;
        }

        /// <summary>
        /// Validate and sanitize department code format
        /// </summary>
        public static string? SanitizeDepartmentCode(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            code = code.Trim().ToUpperInvariant();

            // Must match ABC_123 format
            if (!Regex.IsMatch(code, @"^[A-Z]{3}_[0-9]{3}$"))
                return null;

            return code;
        }

    }
}
