using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace EQS_Tool.Tests
{
    // tests against real certificates
    // certificates contain tenant details so they are NEVER committed: put them in EQS_Tool.Tests/samples/
    // (git ignored) or point EQS_SAMPLES at a folder. Tests return early if no sample is present.
    //
    // expected values come from the file name, so name each sample after what it should produce:
    //   samples/EICR_SAT_900001_13-10-2026.pdf   = EICR, satisfactory, UPRN 900001, dated 13/10/2026
    //   samples/EICR_UNSAT_123456_01-02-2026.pdf
    public class SampleCertificateTests
    {
        public static IEnumerable<object[]> Samples()
        {
            string dir = Environment.GetEnvironmentVariable("EQS_SAMPLES")
                ?? Path.Combine(FindTestProjectDir(), "samples");

            var files = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.pdf") : [];
            if (files.Length == 0)
            {
                yield return new object[] { "" };
                yield break;
            }

            foreach (var f in files) yield return new object[] { f };
        }

        [Theory]
        [MemberData(nameof(Samples))]
        public void Sample_certificate_reads_correctly(string file)
        {
            if (file == "") return; // no samples available on this machine

            // EICR_SAT_900001_13-10-2026.pdf -> type, result, uprn, date
            var parts = Path.GetFileNameWithoutExtension(file).Split('_');
            string type = parts[0], result = parts[1], uprn = parts[2], date = parts[3].Replace('-', '/');

            var config = TestHelpers.LoadAppConfig();
            using var doc = PdfDocument.Open(file);
            var pages = new Page[doc.NumberOfPages + 1];
            for (int i = 1; i <= doc.NumberOfPages; i++) pages[i] = doc.GetPage(i);

            string pageText = string.Join(" ", pages[1].GetWords().Select(w => w.Text));
            Assert.Equal(type, CertificateFunctions.CertificateType(pageText));

            var data = CertificateFunctions.CertificateData(type, pages, config);

            Assert.Equal(uprn, data[1]);
            Assert.Equal(date, CertificateFunctions.ExtractCertificateDate(data[3]));
            Assert.NotEqual("", data[2]);   // certificate number
            Assert.NotEqual("", data[4]);   // address line 1
            Assert.NotEqual("", data[6]);   // postcode
            Assert.NotEqual("", data[7]);   // engineer

            if (type == "EICR")
            {
                Assert.Equal(result, CertificateFunctions.EICRResult(pages[1]));
            }
        }

        private static string FindTestProjectDir()
        {
            string dir = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(dir, "EQS_Tool.Tests.csproj")))
            {
                dir = Path.GetDirectoryName(dir) ?? throw new DirectoryNotFoundException("EQS_Tool.Tests project folder not found");
            }
            return dir;
        }
    }
}
