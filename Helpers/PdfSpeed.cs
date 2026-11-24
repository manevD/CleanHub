using System.Text.RegularExpressions;
using SelectPdf;

namespace CleanHub.Helpers
{
    /// <summary>
    /// Забрзување на HTML → PDF (SelectPdf) без промена на изгледот на фактурата:
    ///  - <script> таговите се отстрануваат пред конверзија (во PDF не прават ништо,
    ///    а секоја фактура порано симнуваше ~7 JS датотеки и ги извршуваше);
    ///  - нема дополнително чекање од 1 секунда по фактура (MinPageLoadTime = 0).
    /// CSS (изгледот) останува ист.
    /// </summary>
    public static partial class PdfSpeed
    {
        [GeneratedRegex(@"<script\b[^>]*>[\s\S]*?</script\s*>", RegexOptions.IgnoreCase)]
        private static partial Regex ScriptTags();

        public static string Prepare(HtmlToPdf converter, string html)
        {
            converter.Options.MinPageLoadTime = 0;
            converter.Options.JavaScriptEnabled = false;
            return string.IsNullOrEmpty(html) ? html : ScriptTags().Replace(html, string.Empty);
        }
    }
}
