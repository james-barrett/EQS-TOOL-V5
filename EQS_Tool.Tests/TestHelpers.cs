using System.Collections.Specialized;
using System.Data;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace EQS_Tool.Tests
{
    internal static class TestHelpers
    {
        public static string LogPath { get; } = Path.Combine(Path.GetTempPath(), "eqs_tool_tests.log");

        // property list like PROPERTIES.xlsx
        public static DataTable AddressTable(params (string Uprn, string Address)[] rows)
        {
            var table = new DataTable();
            table.Columns.Add("Property Reference");
            table.Columns.Add("Property Address");
            foreach (var (uprn, address) in rows)
            {
                table.Rows.Add(uprn, address);
            }
            return table;
        }

        // load the real App.config from the main project so tests use the live rect values
        public static ApplicationConfig LoadAppConfig()
        {
            string? dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "EQS_Tool", "App.config")))
            {
                dir = Path.GetDirectoryName(dir);
            }
            if (dir == null) throw new FileNotFoundException("EQS_Tool/App.config not found above " + AppContext.BaseDirectory);

            var settings = new NameValueCollection();
            foreach (var add in XDocument.Load(Path.Combine(dir, "EQS_Tool", "App.config")).Descendants("add"))
            {
                settings[(string)add.Attribute("key")!] = (string?)add.Attribute("value");
            }
            return ApplicationConfig.Load(settings);
        }

        // a landscape page with the EICR "Satisfactory/Unsatisfactory**" wording, optionally struck through
        // strike: "unsat" = line through Unsatisfactory (a SAT certificate), "sat" = line through Satisfactory, "both", or null
        public static Page EicrResultPage(string? strike, bool addNoiseLines = true)
        {
            var builder = new PdfDocumentBuilder();
            var page = builder.AddPage(PageSize.A4, isPortrait: false);
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);

            // unrelated lines drawn first, like the real layout (table borders, footer glyphs)
            if (addNoiseLines)
            {
                page.DrawLine(new PdfPoint(0, 0), new PdfPoint(0.558, 7.869));
                page.DrawLine(new PdfPoint(150, 229.8), new PdfPoint(400, 229.8), 0.75);
            }

            var letters = page.AddText("Satisfactory/Unsatisfactory**", 10, new PdfPoint(597, 230), font);
            var slash = letters.First(l => l.Value == "/");
            double left = letters.First().GlyphRectangle.Left;
            double right = letters.Last().GlyphRectangle.Right;
            double y = 233; // through the middle of the text

            if (strike == "unsat" || strike == "both")
            {
                page.DrawLine(new PdfPoint(slash.GlyphRectangle.Right + 0.5, y), new PdfPoint(right - 6, y), 2);
            }
            if (strike == "sat" || strike == "both")
            {
                page.DrawLine(new PdfPoint(left, y), new PdfPoint(slash.GlyphRectangle.Left - 0.5, y), 2);
            }

            var doc = PdfDocument.Open(builder.Build());
            return doc.GetPage(1);
        }
    }
}
