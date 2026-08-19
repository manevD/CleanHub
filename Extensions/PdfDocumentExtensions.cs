using SelectPdf;
namespace CleanHub.Extensions
{
    public static class PdfDocumentExtensions
    {
        // Primer: Metod za brzo postavuvanje na marginite vo edna linija
        public static void SetUniformMargins(this PdfDocument doc, int marginSize)
        {
            doc.Margins.Left = marginSize;
            doc.Margins.Right = marginSize;
            doc.Margins.Top = marginSize;
            doc.Margins.Bottom = marginSize;
        }

        // Primer: Metod za proverka dali dokumentot ima stranici
        public static bool HasPages(this PdfDocument doc)
        {
            return doc.Pages != null && doc.Pages.Count > 0;
        }
    }
}

// Koristenje vo kod:
// var doc = new PdfDocument();
// doc.SetUniformMargins(10);