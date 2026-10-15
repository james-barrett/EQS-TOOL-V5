namespace EQS_Tool.Tests
{
    public class CertificateFunctionsTests
    {
        [Theory]
        [InlineData("ELECTRICAL INSTALLATION CONDITION REPORT", "EICR")]
        [InlineData("ELECTRICAL INSTALLATION CERTIFICATE", "EIC")]
        [InlineData("MINOR ELECTRICAL INSTALLATION WORKS CERTIFICATE", "MW")]
        [InlineData("DOMESTIC VISUAL CONDITION REPORT", "VIS")]
        [InlineData("Building Regulations eNotification", "PARTP")]
        [InlineData("COMMISSIONING OF A FIRE DETECTION AND FIRE ALARM SYSTEM", "DFHN")]
        [InlineData("SOMETHING ELSE ENTIRELY", "ERROR")]
        public void CertificateType_detects_each_title(string text, string expected)
        {
            Assert.Equal(expected, CertificateFunctions.CertificateType(text));
        }

        [Fact]
        public void CertificateType_minor_works_mentioning_an_EIC_is_still_minor_works()
        {
            string text = "MINOR ELECTRICAL INSTALLATION WORKS CERTIFICATE ... for new circuits an ELECTRICAL INSTALLATION CERTIFICATE should be issued";
            Assert.Equal("MW", CertificateFunctions.CertificateType(text));
        }

        [Theory]
        [InlineData("13/10/2026", "13/10/2026")]
        [InlineData("( 13/10/2026 )", "13/10/2026")]
        [InlineData("01/02/2025 - 05/02/2025", "05/02/2025")]   // span of testing, end date used
        [InlineData("01/02/2025-05/02/2025", "05/02/2025")]     // old layout, words joined without spaces
        [InlineData("From ( 12/10/2026 ) Until ( 13/10/2026 )", "13/10/2026")]
        [InlineData("12-03-2025", "12/03/2025")]                // dashes in a single date
        [InlineData("1/2/2025", "01/02/2025")]
        [InlineData("13/10/26", "13/10/2026")]
        public void ExtractCertificateDate_handles_known_formats(string raw, string expected)
        {
            Assert.Equal(expected, CertificateFunctions.ExtractCertificateDate(raw));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("N/A")]
        [InlineData("32/13/2025")]
        public void ExtractCertificateDate_returns_null_when_no_valid_date(string raw)
        {
            Assert.Null(CertificateFunctions.ExtractCertificateDate(raw));
        }

        [Fact]
        public void EICRResult_line_through_Unsatisfactory_is_SAT()
        {
            Assert.Equal("SAT", CertificateFunctions.EICRResult(TestHelpers.EicrResultPage("unsat")));
        }

        [Fact]
        public void EICRResult_line_through_Satisfactory_is_UNSAT()
        {
            Assert.Equal("UNSAT", CertificateFunctions.EICRResult(TestHelpers.EicrResultPage("sat")));
        }

        [Fact]
        public void EICRResult_no_strike_line_is_ERROR()
        {
            Assert.Equal("ERROR", CertificateFunctions.EICRResult(TestHelpers.EicrResultPage(null)));
        }

        [Fact]
        public void EICRResult_both_struck_is_ERROR()
        {
            Assert.Equal("ERROR", CertificateFunctions.EICRResult(TestHelpers.EicrResultPage("both")));
        }

        [Fact]
        public void EICRResult_ignores_unrelated_lines_drawn_before_the_result()
        {
            // the old code returned ERROR on the first line it saw, which broke after the NICEIC update
            Assert.Equal("SAT", CertificateFunctions.EICRResult(TestHelpers.EicrResultPage("unsat", addNoiseLines: true)));
        }
    }
}
