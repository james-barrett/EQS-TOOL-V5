namespace EQS_Tool
{
    internal class StringFunctions
    {
        public static float[] AddressFuzzyMatchScore(string addressDB, string? address1, string? address2, string? postcode)
        {
            String[] addressDBSplit = addressDB.Split(',');

            address1 ??= "";
            address2 ??= "";
            postcode ??= "";

            var addressDBAddress = addressDBSplit.SkipLast(1);
            string addressDBString = string.Join(",", addressDBAddress).Replace(",", "").Replace(" ", "").ToUpper();

            string addressCert =
                address1.Replace(",", "").Replace(" ", "").ToUpper() +
                address2.Replace(",", "").Replace(" ", "").ToUpper();

            var scorePC = StringFunctions.LevenshteinDistance(
                addressDBSplit.Last<string>().Trim().ToUpper().Replace(" ", ""),
                postcode.Trim().ToUpper().Replace(" ", "")
                );

            // house/flat numbers from both certificate lines, a flat or building number can be on line 2
            var scoreNum = StringFunctions.LevenshteinDistance(
                new String(addressDBString.Where(Char.IsDigit).ToArray()),
                new String((address1 + address2).Where(Char.IsDigit).ToArray()));

            var scoreADD = StringFunctions.LevenshteinDistance(
                addressDBString,
                addressCert
                );

            return [scoreNum, scorePC, scoreADD];
        }

        private static int LevenshteinDistance(string s1, string s2)
        {
            int len1 = s1.Length;
            int len2 = s2.Length;

            // Initialize a 2D array to store distances
            int[,] dp = new int[len1 + 1, len2 + 1];

            // Initialize the first row and column
            for (int i = 0; i <= len1; i++)
                dp[i, 0] = i;
            for (int j = 0; j <= len2; j++)
                dp[0, j] = j;

            // Fill in the rest of the array
            for (int i = 1; i <= len1; i++)
            {
                for (int j = 1; j <= len2; j++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
                }
            }

            // The final value in the array represents the Levenshtein distance
            return dp[len1, len2];
        }
    }
}
