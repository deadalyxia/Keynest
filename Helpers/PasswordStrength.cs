using System.Text.RegularExpressions;
using System.Windows.Media;

namespace VaultApp.Helpers
{
    public static class PasswordStrength
    {
        public enum Level { None, VeryWeak, Weak, Fair, Strong, VeryStrong }

        public static Level Score(string pw)
        {
            if (string.IsNullOrEmpty(pw)) return Level.None;
            int s = 0;
            if (pw.Length >= 8)  s++;
            if (pw.Length >= 12) s++;
            if (pw.Length >= 16) s++;
            if (Regex.IsMatch(pw, "[a-z]")) s++;
            if (Regex.IsMatch(pw, "[A-Z]")) s++;
            if (Regex.IsMatch(pw, "[0-9]")) s++;
            if (Regex.IsMatch(pw, @"[^a-zA-Z0-9]")) s++;
            return s switch
            {
                0      => Level.VeryWeak,
                1      => Level.VeryWeak,
                2      => Level.Weak,
                3      => Level.Fair,
                4 or 5 => Level.Strong,
                _      => Level.VeryStrong
            };
        }

        public static string Label(Level l) => l switch
        {
            Level.None       => "",
            Level.VeryWeak   => "Very Weak",
            Level.Weak       => "Weak",
            Level.Fair       => "Fair",
            Level.Strong     => "Strong",
            Level.VeryStrong => "Very Strong",
            _                => ""
        };

        public static Color Color(Level l) => l switch
        {
            Level.VeryWeak   => System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44),
            Level.Weak       => System.Windows.Media.Color.FromRgb(0xF8, 0x71, 0x71),
            Level.Fair       => System.Windows.Media.Color.FromRgb(0xFB, 0xBF, 0x24),
            Level.Strong     => System.Windows.Media.Color.FromRgb(0x4A, 0xDE, 0x80),
            Level.VeryStrong => System.Windows.Media.Color.FromRgb(0x22, 0xC5, 0x5E),
            _                => System.Windows.Media.Color.FromRgb(0x2A, 0x2A, 0x42)
        };

        public static int Segments(Level l) => (int)l;
    }
}
