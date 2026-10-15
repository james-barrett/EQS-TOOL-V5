namespace EQS_Tool
{
    public enum LogLevel { Info, Warning, Error }

    public class Logger
    {
        public static void Log(string message, string logFilePath, LogLevel level)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string formatted = $"{timestamp} [{level}] {message}";

            Console.WriteLine(formatted);
            WriteToFile(formatted + Environment.NewLine, logFilePath);
        }

        private static void WriteToFile(string message, string filePath)
        {
            try
            {
                File.AppendAllText(filePath, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log: {ex.Message}");
            }
        }
    }
}