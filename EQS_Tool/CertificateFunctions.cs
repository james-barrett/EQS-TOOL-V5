using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace EQS_Tool
{
    public class CertificateFunctions
    {
        public static string[] CertificateData(string certificateType, Page[] pages, ApplicationConfig appConfig)
        {
            // get needed paths and fields from text bounding boxes
            // returns [JOB REFERENCE, UPRN, CERTIFICATE NUMBER, DATE, ADDRESS LINE 1, ADDRESS LINE 2, POSTCODE, ENGINEER, SUPERVISOR, RESULT, OCCUPIER]
            // every field starts as an empty string so callers never have to null check

            var rects = appConfig.CertificateData[certificateType];
            string[] certificateData = Enumerable.Repeat(string.Empty, 11).ToArray();

            foreach (var word in pages[1].GetWords())
            {
                // first matching rect wins, same order as before
                if (InRect(word, rects.Job)) certificateData[0] += word.Text;
                else if (InRect(word, rects.UPRN)) certificateData[1] += word.Text;
                else if (InRect(word, rects.Certificate)) certificateData[2] += word.Text;
                else if (InRect(word, rects.Date)) certificateData[3] += word.Text + " ";
                else if (InRect(word, rects.Address1)) certificateData[4] += word.Text + " ";
                else if (InRect(word, rects.Address2)) certificateData[5] += word.Text + " ";
                else if (InRect(word, rects.PostCode)) certificateData[6] += word.Text + " ";
                else if (InRect(word, rects.Engineer)) certificateData[7] += word.Text + " ";
                else if (InRect(word, rects.Supervisor)) certificateData[8] += word.Text + " ";
                else if (InRect(word, rects.Occupier)) certificateData[10] += word.Text + " ";
            }

            // DFHN has its date on page 2, guard against single page files
            if (certificateType == "DFHN" && pages.Length > 2 && pages[2] != null)
            {
                foreach (var word in pages[2].GetWords())
                {
                    if (InRect(word, rects.Date)) certificateData[3] += word.Text + " ";
                }
            }

            for (int i = 0; i < certificateData.Length; i++)
            {
                certificateData[i] = certificateData[i].Trim();
            }

            return certificateData;
        }

        private static bool InRect(Word word, ApplicationConfig.RectangleCoordinates rect)
        {
            return word.BoundingBox.Left >= rect.Left &&
                   word.BoundingBox.Bottom >= rect.Bottom &&
                   word.BoundingBox.Right <= rect.Right &&
                   word.BoundingBox.Top <= rect.Top;
        }

        public static string CertificateType(string text)
        {
            // checked in order, most specific first, so a minor works certificate that mentions
            // "ELECTRICAL INSTALLATION CERTIFICATE" in its notes is not picked up as an EIC
            var certificateTypes = new List<(string Key, string Type)>
                {
                    ( "ELECTRICAL INSTALLATION CONDITION REPORT", "EICR" ),
                    ( "MINOR ELECTRICAL INSTALLATION WORKS CERTIFICATE", "MW" ),
                    ( "DOMESTIC VISUAL CONDITION REPORT", "VIS" ),
                    ( "COMMISSIONING OF A FIRE DETECTION", "DFHN" ),
                    ( "eNotification", "PARTP" ),
                    ( "ELECTRICAL INSTALLATION CERTIFICATE", "EIC" )
                };

            foreach (var certificate in certificateTypes)
            {
                if (text.Contains(certificate.Key))
                {
                    return certificate.Type;
                }
            }

            return "ERROR";
        }

        // pull the certificate date out of the date field text
        // handles "13/10/2026", "(13/10/2026)", "01/02/2025 - 05/02/2025" (uses the end date) and "12-03-2025"
        // returns dd/MM/yyyy or null if no valid date found
        public static string? ExtractCertificateDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            var matches = Regex.Matches(text, @"\d{1,2}[/\-.]\d{1,2}[/\-.]\d{2,4}");
            if (matches.Count == 0) return null;

            string candidate = matches[^1].Value.Replace('-', '/').Replace('.', '/');
            string[] formats = ["d/M/yyyy", "dd/MM/yyyy", "d/M/yy", "dd/MM/yy"];

            if (DateTime.TryParseExact(candidate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
            {
                return parsed.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            }

            return null;
        }



        //function to determine the result of the certificate type EICR Satisfactory or Unsatisfactory
        //the PDF does not contain the result as text, the word that does not apply in
        //"Satisfactory/Unsatisfactory" is struck through with a drawn line
        //find that word, find the "/" in it, then find a stroked horizontal line through the word:
        //  line right of the "/" = "Unsatisfactory" struck out = SAT
        //  line left of the "/"  = "Satisfactory" struck out   = UNSAT
        //no positions are hard coded so small layout changes do not break it
        //returns "ERROR" if the word or a strike line cannot be found, or both halves are struck
        public static string EICRResult(Page page)
        {
            var resultWord = page.GetWords().FirstOrDefault(w =>
                w.Text.StartsWith("Satisfactory/Unsatisfactory", StringComparison.OrdinalIgnoreCase));

            if (resultWord == null)
            {
                return "ERROR";
            }

            var slash = resultWord.Letters.FirstOrDefault(l => l.Value == "/");
            if (slash == null)
            {
                return "ERROR";
            }

            var word = resultWord.BoundingBox;
            double slashX = (slash.GlyphRectangle.Left + slash.GlyphRectangle.Right) / 2;

            bool satisfactoryStruck = false;
            bool unsatisfactoryStruck = false;

            foreach (var path in page.Paths)
            {
                if (!path.IsStroked) continue;

                var bounds = path.GetBoundingRectangle();
                if (bounds == null) continue;

                var line = bounds.Value;

                // horizontal line, at least 10pt long, running through the middle of the word
                bool horizontal = line.Height < 2 && line.Width >= 10;
                double lineY = (line.Bottom + line.Top) / 2;
                bool throughWord = lineY >= word.Bottom && lineY <= word.Top;
                bool overlapsWord = line.Right > word.Left && line.Left < word.Right;

                if (!horizontal || !throughWord || !overlapsWord) continue;

                double lineMidX = (line.Left + line.Right) / 2;

                if (lineMidX > slashX)
                {
                    unsatisfactoryStruck = true;
                }
                else
                {
                    satisfactoryStruck = true;
                }
            }

            if (unsatisfactoryStruck && !satisfactoryStruck) return "SAT";
            if (satisfactoryStruck && !unsatisfactoryStruck) return "UNSAT";

            return "ERROR";
        }


    }
}
