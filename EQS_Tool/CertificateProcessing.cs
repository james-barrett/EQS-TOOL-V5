namespace EQS_Tool
{
    public enum CopyResult { Copied, Duplicate, Failed }

    public class CertificateProcessing
    {
        // copy the certificate to its destination share
        // Duplicate = a file with that name is already there (caller moves the local file to DUPLICATE)
        // Failed    = the copy threw, usually the share is unavailable (caller leaves the file to retry next run)
        public static CopyResult ProcessingCertificate(string remotePath, string fileName, string file, string logFilePath)
        {
            string destinationPath = Path.Combine(remotePath, fileName);

            if (File.Exists(destinationPath))
            {
                Logger.Log($"Not copied, {destinationPath} already exists.", logFilePath, LogLevel.Warning);
                return CopyResult.Duplicate;
            }

            try
            {
                File.Copy(file, destinationPath);
                Logger.Log($"Copied {file} to {destinationPath}", logFilePath, LogLevel.Info);
                return CopyResult.Copied;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to copy {file} to {destinationPath}, the shared drive may be unavailable. Exception: {ex.Message}", logFilePath, LogLevel.Error);
                return CopyResult.Failed;
            }
        }
    }
}
