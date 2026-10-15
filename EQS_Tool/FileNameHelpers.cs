namespace EQS_Tool
{
    public class FileNameHelpers()
    {
        public static string MakeValidFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalidChar in invalidChars)
            {
                fileName = fileName.Replace(invalidChar, '_');
            }
            return fileName;
        }

        public static string? GenerateFileName(string certificateType, string[] certificateData, string namingFormat)
        {
            //function to generate file names based on certificate type and naming format
            //expects certificateData[3] to already be a dd/MM/yyyy date (see CertificateFunctions.ExtractCertificateDate)
            //returns null if the UPRN or date are unusable so the caller can move the file to ERROR

            // set variables from certificate data
            string uprn = (certificateData[1] ?? string.Empty).Trim().ToUpper();
            string date = certificateData[3] ?? string.Empty;
            string result = certificateData[9] ?? string.Empty;

            string[] dateArray = date.Split("/");

            if (uprn.Length == 0 || dateArray.Length != 3 || dateArray[2].Length != 4)
            {
                return null;
            }

            string namingConvention = certificateType;
            string suffix = ".pdf";

            if (certificateType == "EICR")
            {
                namingConvention = (uprn.Contains("B") || uprn.Contains("C")) ? "CEICR" : "DEICR";

                if (result == "UNSAT")
                {
                    suffix = "_UNSAT.pdf";
                }
            }

            string fileName = namingFormat
                .Replace("UPRN", uprn)
                .Replace("TYPE", namingConvention)
                .Replace("DD", dateArray[0])
                .Replace("MM", dateArray[1])
                .Replace("YY", dateArray[2].Substring(2)) + suffix;

            return MakeValidFileName(fileName);
        }
    }
}
