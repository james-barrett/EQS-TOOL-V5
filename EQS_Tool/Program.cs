using System.Configuration;
using System.Data;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Page = UglyToad.PdfPig.Content.Page;


namespace EQS_Tool
{
    class Program
    {
        // department sub folders, needed for logic
        static readonly string[] departments = ["EH", "RR"];

        // folders created under each department, files are moved here with a logged reason
        //   ERROR                - unreadable PDF, unknown document, no EICR result, no date, no certificate number
        //   UPRN_ERROR           - UPRN missing or not in PROPERTIES.xlsx
        //   ADDRESS_CHECK_FAILED - certificate address doesn't match PROPERTIES.xlsx
        //   SKIPPED              - draft certificate
        //   WRONG_DEPARTMENT     - empty homes (VOID/EMPTY occupier) certificate outside the EH folder
        //   DUPLICATE            - a file with the new name already exists at the destination
        //   EMAIL_FAILED         - copied to the destination but the email could not be sent
        //   PROCESSED            - renamed copies when AutoMoveFiles is off
        static readonly string[] folders = ["ERROR", "UPRN_ERROR", "ADDRESS_CHECK_FAILED", "WRONG_DEPARTMENT", "SKIPPED", "DUPLICATE", "EMAIL_FAILED", "PROCESSED"];

        static void Main(string[] args)
        {
            string basePath = ResolveBasePath();

            // Set current timestamp for unique file creation
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");

            // Create log dir if non exists
            // Create log file using timestamp
            string[] dirs = [
                Path.Combine(basePath, "_logs"),
                Path.Combine(basePath, "Certificates")
            ];

            string logFilePath = Path.Combine(dirs[0], $"Log-{timestamp}.txt");

            foreach (string dir in dirs)
            {
                var result = DirectoryFunctions.Create(dir);
                Logger.Log(result.message, logFilePath, LogLevel.Info);
            }

            // Load all values from config file or exit.
            ApplicationConfig appConfig = new ApplicationConfig();

            try
            {
                appConfig = ApplicationConfig.Load();
            }
            catch (ConfigurationException ex)
            {
                Logger.Log($"Error processing configuration file: {ex.Message}", logFilePath, LogLevel.Error);
                Helpers.CleanExit();
            }

            string excelAddressListPath = Path.Combine(basePath, "PROPERTIES.xlsx");
            DataTable addressTable = DataTableFunctions.LoadDataTable(excelAddressListPath, logFilePath);

            string certificateDirectory = dirs[1];

            // check and create if missing or exit
            foreach (string dept in departments)
            {
                foreach (string folder in folders)
                {
                    string folderPath = Path.Combine(certificateDirectory, dept, folder);

                    if (Directory.Exists(folderPath)) continue;

                    try
                    {
                        Directory.CreateDirectory(folderPath);
                    }
                    catch (Exception ex)
                    {
                        throw new IOException($"Failed to create directory: {folderPath}", ex);
                    }
                }
            }

            // process only pdf files in root of directory per dept sub folder
            foreach (string department in departments)
            {
                string deptFolderPath = Path.Combine(certificateDirectory, department);
                string[] files = Directory.GetFiles(deptFolderPath, "*.pdf", SearchOption.TopDirectoryOnly);

                Logger.Log($"Processing {department} found {files.Length}.", logFilePath, LogLevel.Info);

                foreach (string file in files)
                {
                    // delay between files so copies to the CIFS share can complete
                    Thread.Sleep(2000);
                    Logger.Log($"-------------------------------------------", logFilePath, LogLevel.Info);
                    Logger.Log($"Processing {file}.", logFilePath, LogLevel.Info);

                    // one bad certificate should never stop the run
                    try
                    {
                        ProcessFile(file, department, deptFolderPath, appConfig, addressTable, logFilePath, timestamp);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Unexpected error processing {file}. Exception: {ex}", logFilePath, LogLevel.Error);
                        MoveTo(file, deptFolderPath, "ERROR", "unexpected error, see exception above", logFilePath);
                    }
                }
            }
        }

        // BasePath from config if set, otherwise the folder above \bin when running from Visual Studio,
        // otherwise the current folder (production)
        static string ResolveBasePath()
        {
            string? configured = ConfigurationManager.AppSettings["BasePath"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            string pathCur = Directory.GetCurrentDirectory();
            string binSegment = Path.DirectorySeparatorChar + "bin";
            int binIndex = pathCur.IndexOf(binSegment + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

            if (binIndex < 0 && pathCur.EndsWith(binSegment, StringComparison.OrdinalIgnoreCase))
            {
                binIndex = pathCur.Length - binSegment.Length;
            }

            return binIndex >= 0 ? pathCur.Substring(0, binIndex) : pathCur;
        }

        static void ProcessFile(string file, string department, string deptFolderPath, ApplicationConfig appConfig,
            DataTable addressTable, string logFilePath, string timestamp)
        {
            PdfDocument doc;

            // try to open the pdf, on error move to ERROR folder
            try
            {
                doc = PdfDocument.Open(file);
            }
            catch (Exception ex)
            {
                MoveTo(file, deptFolderPath, "ERROR", $"could not read PDF: {ex.Message}", logFilePath);
                return;
            }

            // dispose before any move/delete so the file isn't locked
            string? fileName;
            string[] certificateData;
            string certificateType;

            using (doc)
            {
                int pageCount = doc.NumberOfPages;
                Page[] pages = new Page[pageCount + 1];

                // use first member of array at index 1 to keep logic simple as pdf page index starts at 1
                for (int i = 1; i < pageCount + 1; i++)
                {
                    pages[i] = doc.GetPage(i);
                }

                // use first page of pdf to get certificate type
                Page page = pages[1];
                string pageText = string.Join(" ", page.GetWords().Select(w => w.Text));
                certificateType = CertificateFunctions.CertificateType(pageText);

                // if enabled various tools below
                if (appConfig.Settings.PrintTextCoordinates)
                {
                    DevTools.PrintCoordsToConsole(page);
                }

                // if CertificateType returned ERROR we have an invalid file
                if (certificateType == "ERROR")
                {
                    MoveTo(file, deptFolderPath, "ERROR", "not a recognised certificate type", logFilePath, doc);
                    return;
                }

                if (appConfig.Settings.DrawRectsToPDF)
                {
                    DevTools.DrawRectsToPDF(file, deptFolderPath, certificateType, appConfig, timestamp);
                }

                // CertificateData uses certificateType to locate correct Rect coords from config
                // then parses pdf to retrieve data from those locations.
                certificateData = CertificateFunctions.CertificateData(certificateType, pages, appConfig);

                if (certificateType == "EICR")
                {
                    string result = CertificateFunctions.EICRResult(page);

                    if (result == "ERROR")
                    {
                        MoveTo(file, deptFolderPath, "ERROR", "EICR result (Satisfactory/Unsatisfactory strike-through) not found", logFilePath, doc);
                        return;
                    }

                    certificateData[9] = result;
                }
            }

            if (appConfig.Settings.PrintDataToConsole)
            {
                foreach (string value in certificateData)
                {
                    Console.WriteLine(value);
                }
            }

            string addressLine1 = certificateData[4];
            string addressLine2 = certificateData[5];
            string postcode = certificateData[6];

            // if certificateType is DFHN we need to get UPRN using the address
            // the address should be perfect from EQS checks so should be no issues
            if (certificateType == "DFHN")
            {
                string address = $"{addressLine1} {addressLine2} {postcode}";
                certificateData[1] = DataTableFunctions.GetUPRNFromDataTable(addressTable, address, logFilePath);
            }

            // if certificateType is PARTP we need to get UPRN using the address
            // the address is incomplete and does not match the certificate usually missing county
            if (certificateType == "PARTP")
            {
                certificateData[1] = DataTableFunctions.GetUPRNFromDataTable(addressTable, addressLine1, logFilePath);
            }

            // set UPRN, either from initial load of certificateData or amended via if DFHN or PARTP
            string uprn = certificateData[1];

            // date as dd/MM/yyyy, if testing was carried out over a span of time the end date is used
            string? date = CertificateFunctions.ExtractCertificateDate(certificateData[3]);

            if (date == null)
            {
                MoveTo(file, deptFolderPath, "ERROR", $"no valid date found (read \"{certificateData[3]}\")", logFilePath);
                return;
            }

            certificateData[3] = date;

            // get address from DB based on UPRN
            // if UPRN_ERROR returned from function, move to UPRN_ERROR folder
            string addressDB = DataTableFunctions.GetAddressFromDataTable(addressTable, uprn, logFilePath);

            if (addressDB == "UPRN_ERROR")
            {
                MoveTo(file, deptFolderPath, "UPRN_ERROR", $"UPRN \"{uprn}\" not found", logFilePath);
                return;
            }

            // not DFHN or PARTP as they have no UPRN on the certificate to check the address against
            if (certificateType != "DFHN" && certificateType != "PARTP")
            {
                // use helper function AddressFuzzyMatchScore to validate address
                float[] scores = StringFunctions.AddressFuzzyMatchScore(addressDB, addressLine1, addressLine2, postcode);

                if (scores[0] != 0 || scores[1] != 0 || scores[2] >= appConfig.Settings.ScoreThreshold)
                {
                    Logger.Log($"Address check failed.", logFilePath, LogLevel.Warning);
                    Logger.Log($"Expected: {addressDB}", logFilePath, LogLevel.Warning);
                    Logger.Log($"Received: {addressLine1} {addressLine2} {postcode}", logFilePath, LogLevel.Warning);
                    MoveTo(file, deptFolderPath, "ADDRESS_CHECK_FAILED", $"scores number={scores[0]} postcode={scores[1]} address={scores[2]}", logFilePath);
                    return;
                }
            }

            // PARTP does not contain occupier or certificate number details
            if (certificateType != "PARTP")
            {
                // make sure we aren't processing Empty Homes files from other department folders
                if (department != "EH" && (certificateData[10].ToUpper().Contains("EMPTY") || certificateData[10].ToUpper().Contains("VOID")))
                {
                    MoveTo(file, deptFolderPath, "WRONG_DEPARTMENT", $"occupier is \"{certificateData[10]}\", Empty Homes certificates belong in the EH folder", logFilePath);
                    return;
                }

                if (certificateData[2].Length == 0)
                {
                    MoveTo(file, deptFolderPath, "ERROR", "certificate number not found", logFilePath);
                    return;
                }

                if (certificateData[2].ToUpper().Contains("DRAFT"))
                {
                    MoveTo(file, deptFolderPath, "SKIPPED", "draft certificate", logFilePath);
                    return;
                }
            }

            fileName = FileNameHelpers.GenerateFileName(certificateType, certificateData, appConfig.Settings.NamingFormat);

            if (fileName == null)
            {
                MoveTo(file, deptFolderPath, "ERROR", "could not build file name from UPRN and date", logFilePath);
                return;
            }

            if (!appConfig.Settings.AutoMoveFiles)
            {
                string processedFilePath = Path.Combine(deptFolderPath, "PROCESSED", fileName);

                if (File.Exists(processedFilePath))
                {
                    MoveTo(file, deptFolderPath, "DUPLICATE", $"{processedFilePath} already exists", logFilePath);
                    return;
                }

                try
                {
                    File.Copy(file, processedFilePath);
                    File.Delete(file);
                    Logger.Log($"File {fileName} moved to PROCESSED directory.", logFilePath, LogLevel.Info);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error processing {file}. Exception: {ex.Message}", logFilePath, LogLevel.Error);
                }
                return;
            }

            // work out destination and whether to email, based on department and certificate type
            bool isUnsat = certificateType == "EICR" && certificateData[9] == "UNSAT";
            bool isEH = department == "EH";

            string destination = isUnsat ? appConfig.Paths.TGPUNSATFilePath
                : isEH ? appConfig.Paths.TGPEHFilePath
                : appConfig.Paths.TGPRRFilePath;

            bool sendEmail = !isUnsat && (certificateType == "EICR"
                ? (isEH ? appConfig.Settings.AutoEmailEHEICR : appConfig.Settings.AutoEmailRREICR)
                : (isEH ? appConfig.Settings.AutoEmailEHOther : appConfig.Settings.AutoEmailRROther));

            string emailAddress = isEH ? appConfig.Settings.EHEmailAddress : appConfig.Settings.RREmailAddress;

            CopyResult copyResult = CertificateProcessing.ProcessingCertificate(destination, fileName, file, logFilePath);

            if (copyResult == CopyResult.Duplicate)
            {
                MoveTo(file, deptFolderPath, "DUPLICATE", $"{fileName} already exists in {destination}", logFilePath);
                return;
            }

            if (copyResult == CopyResult.Failed)
            {
                // leave the file where it is so it is retried on the next run
                return;
            }

            if (sendEmail && !Email.Send(emailAddress, file, fileName, $"{addressLine1} {addressLine2} {postcode}", logFilePath))
            {
                MoveTo(file, deptFolderPath, "EMAIL_FAILED", $"copied to {destination} but email to {emailAddress} failed", logFilePath);
                return;
            }

            try
            {
                File.Delete(file);
                Logger.Log($"Local file {file} Deleted.", logFilePath, LogLevel.Info);
            }
            catch (Exception ex)
            {
                Logger.Log($"File actions not completed. Exception: {ex}.", logFilePath, LogLevel.Error);
            }
        }

        // move a file into one of the department folders and log why
        // pass the open PdfDocument (if any) so it is released before the move
        static void MoveTo(string file, string deptFolderPath, string folder, string reason, string logFilePath, PdfDocument? openDoc = null)
        {
            openDoc?.Dispose();

            string destinationPath = Path.Combine(deptFolderPath, folder, Path.GetFileName(file));

            if (File.Exists(destinationPath))
            {
                Logger.Log($"{Path.GetFileName(file)} not moved to {folder} ({reason}), a file with that name is already there.", logFilePath, LogLevel.Warning);
                return;
            }

            try
            {
                File.Move(file, destinationPath);
                Logger.Log($"Moved {Path.GetFileName(file)} to {folder}: {reason}.", logFilePath, LogLevel.Warning);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to move {Path.GetFileName(file)} to {folder} ({reason}). Exception: {ex.Message}", logFilePath, LogLevel.Error);
            }
        }
    }
}
