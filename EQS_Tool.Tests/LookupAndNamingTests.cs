namespace EQS_Tool.Tests
{
    public class DataTableFunctionsTests
    {
        private static readonly System.Data.DataTable Table = TestHelpers.AddressTable(
            ("100", "1 High Street, Mocktown, Fakeshire, ZZ2 1AA"),
            ("111", "11 High Street, Mocktown, Fakeshire, ZZ2 1AB"),
            ("121", "21 High Street, Mocktown, Fakeshire, ZZ2 1AC"),
            ("200", "Flat 1, 5 Mill Lane, Testby, ZZ1 2AA"),
            ("201", "Flat 1, 5 Mill Lane, Mockley, ZZ1 3AA"),
            ("O'1", "8 Example View, Sample Road, Testby, Mocktown, Fakeshire, ZZ9 9ZZ"));

        private static string Uprn(string address) =>
            DataTableFunctions.GetUPRNFromDataTable(Table, address, TestHelpers.LogPath);

        [Fact]
        public void UPRN_exact_address_match()
        {
            Assert.Equal("100", Uprn("1 High Street Mocktown Fakeshire ZZ2 1AA"));
        }

        [Fact]
        public void UPRN_partial_address_missing_county_matches_when_unique()
        {
            // PARTP addresses are usually incomplete
            Assert.Equal("100", Uprn("1 High Street, Mocktown"));
        }

        [Fact]
        public void UPRN_1_High_Street_does_not_match_11_or_21()
        {
            Assert.Equal("100", Uprn("1 High Street"));
            Assert.Equal("111", Uprn("11 High Street"));
        }

        [Fact]
        public void UPRN_ambiguous_address_is_an_error_not_a_guess()
        {
            Assert.Equal("UPRN_ERROR", Uprn("Flat 1, 5 Mill Lane"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void UPRN_empty_address_never_matches_the_first_row(string address)
        {
            // the old Contains("") check matched every row and returned row 1
            Assert.Equal("UPRN_ERROR", Uprn(address));
        }

        [Fact]
        public void Address_lookup_by_UPRN()
        {
            Assert.StartsWith("11 High Street", DataTableFunctions.GetAddressFromDataTable(Table, "111", TestHelpers.LogPath));
        }

        [Fact]
        public void Address_lookup_unknown_or_empty_UPRN_is_an_error()
        {
            Assert.Equal("UPRN_ERROR", DataTableFunctions.GetAddressFromDataTable(Table, "999", TestHelpers.LogPath));
            Assert.Equal("UPRN_ERROR", DataTableFunctions.GetAddressFromDataTable(Table, "", TestHelpers.LogPath));
        }

        [Fact]
        public void Address_lookup_UPRN_with_a_quote_does_not_crash()
        {
            Assert.StartsWith("8 Example View", DataTableFunctions.GetAddressFromDataTable(Table, "O'1", TestHelpers.LogPath));
        }
    }

    public class StringFunctionsTests
    {
        private const string Db = "8 Example View, Sample Road, Testby, Mocktown, Fakeshire, ZZ9 9ZZ";

        [Fact]
        public void Matching_address_scores_zero()
        {
            var s = StringFunctions.AddressFuzzyMatchScore(Db, "8 Example View, Sample Road, Testby, Mocktown,", "Fakeshire", "ZZ9 9ZZ");
            Assert.Equal(0, s[0]);
            Assert.Equal(0, s[1]);
            Assert.Equal(0, s[2]);
        }

        [Fact]
        public void Wrong_house_number_or_postcode_is_detected()
        {
            var s = StringFunctions.AddressFuzzyMatchScore(Db, "9 Example View, Sample Road, Testby, Mocktown,", "Fakeshire", "ZZ9 9ZY");
            Assert.NotEqual(0, s[0]);
            Assert.NotEqual(0, s[1]);
        }

        [Fact]
        public void Building_number_on_line_2_still_matches()
        {
            var s = StringFunctions.AddressFuzzyMatchScore("Flat 2, 10 Mill Lane, Testby, ZZ1 4AA", "Flat 2,", "10 Mill Lane, Testby", "ZZ1 4AA");
            Assert.Equal(0, s[0]);
        }

        [Fact]
        public void Missing_fields_do_not_crash()
        {
            var s = StringFunctions.AddressFuzzyMatchScore(Db, null, null, null);
            Assert.NotEqual(0, s[1]);
        }
    }

    public class FileNameHelpersTests
    {
        private static string[] Data(string uprn, string date, string result = "")
        {
            var d = Enumerable.Repeat(string.Empty, 11).ToArray();
            d[1] = uprn;
            d[3] = date;
            d[9] = result;
            return d;
        }

        [Theory]
        [InlineData("900001", "DEICR")]
        [InlineData("B12345", "CEICR")]
        [InlineData("c12345", "CEICR")]
        public void EICR_naming(string uprn, string type)
        {
            Assert.Equal($"{uprn.ToUpper()}_{type}_13-10-26.pdf",
                FileNameHelpers.GenerateFileName("EICR", Data(uprn, "13/10/2026", "SAT"), "UPRN_TYPE_DD-MM-YY"));
        }

        [Fact]
        public void UNSAT_EICR_gets_suffix()
        {
            Assert.Equal("900001_DEICR_13-10-26_UNSAT.pdf",
                FileNameHelpers.GenerateFileName("EICR", Data("900001", "13/10/2026", "UNSAT"), "UPRN_TYPE_DD-MM-YY"));
        }

        [Fact]
        public void Other_types_use_type_name()
        {
            Assert.Equal("900001_MW_01-02-25.pdf",
                FileNameHelpers.GenerateFileName("MW", Data("900001", "01/02/2025"), "UPRN_TYPE_DD-MM-YY"));
        }

        [Theory]
        [InlineData("", "13/10/2026")]
        [InlineData("900001", "")]
        [InlineData("900001", "13/10/26")]
        [InlineData("900001", "13-10-2026")]
        public void Bad_UPRN_or_date_returns_null_instead_of_crashing(string uprn, string date)
        {
            Assert.Null(FileNameHelpers.GenerateFileName("EICR", Data(uprn, date), "UPRN_TYPE_DD-MM-YY"));
        }
    }
}
