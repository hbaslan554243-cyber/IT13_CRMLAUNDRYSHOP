namespace CleanWaveCRM.Desktop
{
    /// <summary>
    /// Shared visual palette: soft blue / white / pastel tones to read as
    /// clean and fresh, matching a laundry shop's brand feel. Applied to
    /// Login, the main shell, and the Dashboard; other screens can adopt
    /// these colors incrementally.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Primary = ColorTranslator.FromHtml("#42A5F5");      // soft blue
        public static readonly Color PrimaryDark = ColorTranslator.FromHtml("#1E88E5");  // deeper blue for sidebar/headers
        public static readonly Color PrimaryDarker = ColorTranslator.FromHtml("#1565C0");
        public static readonly Color Background = ColorTranslator.FromHtml("#F4FAFF");   // near-white pastel blue
        public static readonly Color CardBackground = Color.White;
        public static readonly Color Accent = ColorTranslator.FromHtml("#BBDEFB");       // pastel blue accent
        public static readonly Color TextDark = ColorTranslator.FromHtml("#263238");
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#78909C");
        public static readonly Color Success = ColorTranslator.FromHtml("#66BB6A");
        public static readonly Color Danger = ColorTranslator.FromHtml("#EF5350");
        public static readonly Color Warning = ColorTranslator.FromHtml("#FFA726");

        public static readonly Font TitleFont = new("Segoe UI", 18, FontStyle.Bold);
        public static readonly Font HeaderFont = new("Segoe UI", 13, FontStyle.Bold);
        public static readonly Font BodyFont = new("Segoe UI", 9.5f);
    }
}
