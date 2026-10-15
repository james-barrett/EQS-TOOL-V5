using System.Runtime.InteropServices;

namespace EQS_Tool
{
    internal class Email
    {
        // sends through the locally installed Outlook
        // late bound (dynamic) rather than a COM reference so the project builds with the
        // dotnet CLI / VS Code and on machines without Outlook; only works on Windows with Outlook
        // returns false if the email could not be sent so the caller can keep the file
        public static bool Send(string addresses, string filePath, string fileName, string subject, string logFilePath)
        {
            if (!OperatingSystem.IsWindows())
            {
                Logger.Log("Email is only supported on Windows with Outlook installed.", logFilePath, LogLevel.Error);
                return false;
            }

            dynamic? outlookApp = null;
            dynamic? mailItem = null;

            try
            {
                Type? outlookType = Type.GetTypeFromProgID("Outlook.Application");
                if (outlookType == null)
                {
                    Logger.Log("Outlook is not installed, email not sent.", logFilePath, LogLevel.Error);
                    return false;
                }

                outlookApp = Activator.CreateInstance(outlookType);
                mailItem = outlookApp!.CreateItem(0); // 0 = olMailItem

                // Split the addresses string into an array of email addresses
                foreach (var address in addresses.Split(',').Select(a => a.Trim()).Where(a => a.Length > 0))
                {
                    dynamic recipient = mailItem!.Recipients.Add(address);
                    recipient.Resolve();
                    Marshal.ReleaseComObject(recipient); // Release the recipient object after each addition
                }

                mailItem!.Subject = subject;
                mailItem.Body = "Please find attached.";

                int position = mailItem.Body.Length + 1;
                const int olByValue = 1;

                dynamic attachment = mailItem.Attachments.Add(filePath, olByValue, position, fileName);

                mailItem.Save();
                mailItem.Send();

                Marshal.ReleaseComObject(attachment);
                Logger.Log($"Emailed {fileName} to {addresses}.", logFilePath, LogLevel.Info);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to email {fileName} to {addresses}. Exception: {ex.Message}", logFilePath, LogLevel.Error);
                return false;
            }
            finally
            {
                // Release COM objects
                if (mailItem != null) Marshal.ReleaseComObject(mailItem);
                if (outlookApp != null) Marshal.ReleaseComObject(outlookApp);
            }
        }
    }
}
