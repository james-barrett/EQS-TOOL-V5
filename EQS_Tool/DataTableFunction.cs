using Sylvan.Data.Excel;
using System.Data;
using System.Text.RegularExpressions;


namespace EQS_Tool
{
    public class DataTableFunctions
    {
        private const string UprnColumn = "Property Reference";
        private const string AddressColumn = "Property Address";

        public static DataTable LoadDataTable(string excelFilePath, string logFilePath)
        {
            if (!File.Exists(excelFilePath))
            {
                string message = "Missing dependency address file DB, exiting.....";
                Logger.Log(message, logFilePath, LogLevel.Error);
                Helpers.CleanExit();
            }

            try
            {
                DataTable addressTable = new DataTable();
                using (var excelReader = ExcelDataReader.Create(excelFilePath))
                {
                    addressTable.Load(excelReader);
                }

                // check the columns once here rather than every certificate failing the same way
                foreach (string column in new[] { UprnColumn, AddressColumn })
                {
                    if (!addressTable.Columns.Contains(column))
                    {
                        Logger.Log($"PROPERTIES.xlsx has no \"{column}\" column, exiting.....", logFilePath, LogLevel.Error);
                        Helpers.CleanExit();
                    }
                }

                string message = "Address data loaded successfully.";
                Logger.Log(message, logFilePath, LogLevel.Info);
                return addressTable;
            }
            catch (Exception ex)
            {
                string message = $"Error loading address data, quitting. Exception: {ex.Message}.";
                Logger.Log(message, logFilePath, LogLevel.Error);
                Helpers.CleanExit();
                return null!;
            }
        }
        public static string GetAddressFromDataTable(DataTable addressTable, string uprn, string logFilePath)
        {
            string address = "UPRN_ERROR";

            if (string.IsNullOrWhiteSpace(uprn))
            {
                Logger.Log("No UPRN found on certificate.", logFilePath, LogLevel.Warning);
                return address;
            }

            try
            {
                // LINQ rather than DataTable.Select so a quote in the UPRN can't break the query
                var match = addressTable.AsEnumerable()
                    .FirstOrDefault(row => string.Equals(row[UprnColumn]?.ToString()?.Trim(), uprn.Trim(), StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    address = match[AddressColumn]?.ToString() ?? "UPRN_ERROR";
                }
                else
                {
                    Logger.Log($"No matching address found for UPRN: {uprn}", logFilePath, LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error retrieving address for UPRN: {uprn}. Exception: {ex.Message}", logFilePath, LogLevel.Error);
            }

            return address;
        }

        // find the UPRN for certificates that don't carry one (DFHN, PARTP)
        // these certificates skip the address check, so a wrong match would file the certificate
        // against the wrong property without anyone noticing. Only accept a single unambiguous match:
        //   1. exact match on the normalised address
        //   2. otherwise the DB address starts with the certificate address at a word boundary
        //      (PARTP addresses are usually missing the county), and only one row matches
        // anything else returns UPRN_ERROR
        public static string GetUPRNFromDataTable(DataTable addressTable, string address, string logFilePath)
        {
            string uprn = "UPRN_ERROR";
            string normalizedAddress = NormalizeString(address);

            if (normalizedAddress.Length == 0)
            {
                Logger.Log("No address found on certificate, can't look up UPRN.", logFilePath, LogLevel.Warning);
                return uprn;
            }

            try
            {
                var rows = addressTable.AsEnumerable()
                    .Select(row => (Row: row, Address: NormalizeString(row[AddressColumn]?.ToString() ?? string.Empty)))
                    .ToList();

                var matches = rows
                    .Where(r => r.Address.Equals(normalizedAddress, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count == 0)
                {
                    matches = rows
                        .Where(r => StartsWithWholeWords(r.Address, normalizedAddress))
                        .ToList();
                }

                if (matches.Count == 1)
                {
                    uprn = matches[0].Row[UprnColumn]?.ToString() ?? "UPRN_ERROR";
                }
                else if (matches.Count == 0)
                {
                    Logger.Log($"No matching UPRN found for address: {address}", logFilePath, LogLevel.Warning);
                }
                else
                {
                    Logger.Log($"{matches.Count} properties match address: {address}, not guessing.", logFilePath, LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error retrieving UPRN for address: {address}. Exception: {ex.Message}", logFilePath, LogLevel.Error);
            }

            return uprn;
        }

        // "1 HIGH STREET MOCKTOWN" starts with "1 HIGH STREET" - but "11 HIGH STREET" does not,
        // and "1 HIGH STREETLY" does not either
        private static bool StartsWithWholeWords(string fullAddress, string prefix)
        {
            if (!fullAddress.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            return fullAddress.Length == prefix.Length || fullAddress[prefix.Length] == ' ';
        }

        public static string NormalizeString(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return Regex.Replace(input.Replace(",", " ").Trim(), @"\s+", " ");
        }
    }
}
