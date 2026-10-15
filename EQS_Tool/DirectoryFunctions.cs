namespace EQS_Tool
{
    internal class DirectoryFunctions
    {
        public static (bool success, string message) Create(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                return (false, $"Directory '{directoryPath}' already exists.");
            }

            try
            {
                Directory.CreateDirectory(directoryPath);
                return (true, $"Directory '{directoryPath}' created successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Error creating directory: {ex.Message}");
            }
        }
    }
}


