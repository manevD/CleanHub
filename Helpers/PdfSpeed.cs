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

        /// <summary>
        /// A4 поставки за _DocumentDetailPartialPrint (истите како во „Креирај фактура“):
        /// страната е широка 794px = A4, па висината од 277mm во CSS е точно до дното на листот
        /// и делот од испрекинатата линија оди најдолу.
        /// </summary>
        public static void UseA4(HtmlToPdf converter)
        {
            converter.Options.PdfPageSize = PdfPageSize.A4;
            converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
            converter.Options.MarginTop = 5;
            converter.Options.MarginBottom = 5;
            converter.Options.MarginLeft = 5;
            converter.Options.MarginRight = 5;
            converter.Options.WebPageWidth = 794;
            converter.Options.WebPageHeight = 0;
            converter.Options.WebPageFixedSize = false;
            converter.Options.AutoFitWidth = HtmlToPdfPageFitMode.ShrinkOnly;
            converter.Options.AutoFitHeight = HtmlToPdfPageFitMode.NoAdjustment;
        }

        public static string Prepare(HtmlToPdf converter, string html)
        {
            converter.Options.MinPageLoadTime = 0;
            converter.Options.JavaScriptEnabled = false;
            return string.IsNullOrEmpty(html) ? html : ScriptTags().Replace(html, string.Empty);
        }
    }
}
