namespace EQS_Tool
{
    public class Helpers
    {
        public static void CleanExit()
        {
            // only wait for a key when someone is at the console, so scheduled runs don't hang
            if (Environment.UserInteractive && !Console.IsInputRedirected)
            {
                Console.WriteLine("Press any key to exit.....");
                Console.ReadKey();
            }
            Environment.Exit(-1);
        }
    }
}
