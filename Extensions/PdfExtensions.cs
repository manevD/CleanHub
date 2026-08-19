using SelectPdf;

namespace CleanHub.Extensions
{
    public static class PdfExtensions
    {
        /// <summary>
        /// Spojuva lista od PdfDocument objekti vo eden nov PdfDocument.
        /// </summary>
        public static PdfDocument Merge(this IEnumerable<PdfDocument> documents)
        {
            var finalDoc = new PdfDocument();

            foreach (var doc in documents)
            {
                for (int i = 0; i < doc.Pages.Count; i++)
                {
                    // Ja dodava sekoja stranica od tekovniot dokument vo finalniot
                    finalDoc.AddPage(doc.Pages[i]);
                }
            }

            return finalDoc;
        }
    }
}
