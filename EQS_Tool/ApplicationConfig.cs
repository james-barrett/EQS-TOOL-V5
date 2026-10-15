using System.Collections.Specialized;
using System.Configuration;

namespace EQS_Tool
{
    public class ApplicationConfig
    {
        public class GeneralSettings
        {
            public float ScoreThreshold { get; set; }
            public bool AutoMoveFiles { get; set; }
            public bool AutoEmailEHEICR { get; set; }
            public bool AutoEmailRREICR { get; set; }
            public bool AutoEmailEHOther { get; set; }
            public bool AutoEmailRROther { get; set; }
            public string EHEmailAddress { get; set; } = string.Empty;
            public string RREmailAddress { get; set; } = string.Empty;
            public string NamingFormat { get; set; } = string.Empty;
            public string BasePath { get; set; } = string.Empty;
            public bool PrintTextCoordinates { get; set; }
            public bool DrawRectsToPDF { get; set; }
            public bool PrintDataToConsole { get; set; }
        }

        public class FilePaths
        {
            public string TGPEHFilePath { get; set; } = string.Empty;
            public string TGPRRFilePath { get; set; } = string.Empty;
            public string TGPUNSATFilePath { get; set; } = string.Empty;
        }

        public class RectangleCoordinates
        {
            public int[] Coordinates { get; private set; } = new int[] { 0, 0, 0, 0 };

            public RectangleCoordinates(string? value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    Coordinates = new int[] { 0, 0, 0, 0 };
                    return;
                }

                var coords = value.Split(',');
                if (coords.Length != 4)
                {
                    throw new ConfigurationException($"Invalid rectangle coordinates: {value}");
                }

                try
                {
                    Coordinates = coords.Select(int.Parse).ToArray();
                }
                catch (Exception ex)
                {
                    throw new ConfigurationException($"Failed to parse rectangle coordinates: {value}", ex);
                }
            }

            // Convenience properties that match Rectangle properties
            public int Left => Coordinates[0];
            public int Bottom => Coordinates[1];
            public int Right => Coordinates[2];
            public int Top => Coordinates[3];

            // Allow array-style access
            public int this[int index]
            {
                get
                {
                    if (index < 0 || index >= Coordinates.Length)
                        throw new IndexOutOfRangeException();
                    return Coordinates[index];
                }
            }
        }

        public class CertificateRectData
        {
            public RectangleCoordinates Job { get; set; } = new RectangleCoordinates(null);
            public RectangleCoordinates UPRN { get; set; } = new RectangleCoordinates(null);
            public RectangleCoordinates Certificate { get; set; } = new RectangleCoordinates(null);
            public RectangleCoordinates Date { get; set; } = new RectangleCoordinates(null);
            public RectangleCoordinates Address1 { get; set; } = new RectangleCoordinates(null);
            public RectangleCoordinates Address2 { get; set; } = new RectangleCoordinates(null);
            public RectangleCoordinates PostCode { get; set; } = new RectangleCoordinates(null);
            public RectangleCoordinates Engineer { get; set; } = new RectangleCoordinates(null);
            public RectangleCoordinates Supervisor { get; set; } = new RectangleCoordinates(null);
            public RectangleCoordinates Occupier { get; set; } = new RectangleCoordinates(null);
        }

        public GeneralSettings Settings { get; private set; } = new GeneralSettings();

        public FilePaths Paths { get; private set; } = new FilePaths();

        public Dictionary<string, CertificateRectData> CertificateData { get; private set; } = new Dictionary<string, CertificateRectData>();

        public static ApplicationConfig Load() => Load(ConfigurationManager.AppSettings);

        // overload so tests can load settings from a copy of App.config
        public static ApplicationConfig Load(NameValueCollection appSettings)
        {
            if (appSettings == null)
                throw new ConfigurationException("AppSettings section not found in configuration file");

            var config = new ApplicationConfig
            {
                Settings = LoadGeneralSettings(appSettings),
                Paths = LoadFilePaths(appSettings),
                CertificateData = LoadCertificateData(appSettings)
            };

            return config;
        }

        private static GeneralSettings LoadGeneralSettings(NameValueCollection appSettings)
        {
            var settings = new GeneralSettings();

            try
            {
                // Parse score threshold
                if (!float.TryParse(appSettings["ScoreThreshold"], out float threshold))
                {
                    throw new ConfigurationException("Invalid ScoreThreshold value");
                }
                settings.ScoreThreshold = threshold;



                // Parse boolean settings#
                settings.AutoMoveFiles = IsYes(appSettings["AutoMoveFiles"]);

                settings.AutoEmailEHEICR = IsYes(appSettings["AutoEmailEHCertificatesEICR"]);
                settings.AutoEmailRREICR = IsYes(appSettings["AutoEmailRRCertificatesEICR"]);

                settings.AutoEmailEHOther = IsYes(appSettings["AutoEmailEHCertificatesOther"]);
                settings.AutoEmailRROther = IsYes(appSettings["AutoEmailRRCertificatesOther"]);

                settings.PrintTextCoordinates = IsYes(appSettings["PrintTextCoordinates"]);
                settings.DrawRectsToPDF = IsYes(appSettings["DrawRectsToPDF"]);
                settings.PrintDataToConsole = IsYes(appSettings["PrintDataToConsole"]);

                // Parse email addresses
                if (settings.AutoEmailEHEICR || settings.AutoEmailEHOther)
                {
                    settings.EHEmailAddress = ValidateEmailAddress(appSettings["EHEmailAddress"], "EH");
                }

                if (settings.AutoEmailRREICR || settings.AutoEmailRROther)
                {
                    settings.RREmailAddress = ValidateEmailAddress(appSettings["RREmailAddress"], "RR");
                }

                // Parse naming format
                settings.NamingFormat = appSettings["NamingFormat"] ?? "UPRN_TYPE_DD-MM-YY";

                // optional, folder holding Certificates/, _logs/ and PROPERTIES.xlsx. Blank = work it out from the exe location
                settings.BasePath = appSettings["BasePath"] ?? string.Empty;

                return settings;
            }
            catch (Exception ex) when (ex is not ConfigurationException)
            {
                throw new ConfigurationException("Failed to load general settings", ex);
            }
        }

        private static FilePaths LoadFilePaths(NameValueCollection appSettings)
        {
            try
            {
                return new FilePaths
                {
                    TGPEHFilePath = GetRequiredSetting(appSettings, "TGPEHFilePath"),
                    TGPRRFilePath = GetRequiredSetting(appSettings, "TGPRRFilePath"),
                    TGPUNSATFilePath = GetRequiredSetting(appSettings, "TGPUNSATFilePath"),
                };
            }
            catch (Exception ex)
            {
                throw new ConfigurationException("Failed to load file paths", ex);
            }
        }

        private static Dictionary<string, CertificateRectData> LoadCertificateData(NameValueCollection appSettings)
        {
            var certificateTypes = new[] { "EICR", "EIC", "MW", "VIS", "PARTP", "DFHN" };
            var result = new Dictionary<string, CertificateRectData>();

            foreach (var certType in certificateTypes)
            {
                try
                {
                    result[certType] = new CertificateRectData
                    {
                        Job = new RectangleCoordinates(appSettings[$"{certType}_JOB"]),
                        UPRN = new RectangleCoordinates(appSettings[$"{certType}_UPRN"]),
                        Certificate = new RectangleCoordinates(appSettings[$"{certType}_CERT"]),
                        Date = new RectangleCoordinates(appSettings[$"{certType}_DATE"]),
                        Address1 = new RectangleCoordinates(appSettings[$"{certType}_ADD1"]),
                        Address2 = new RectangleCoordinates(appSettings[$"{certType}_ADD2"]),
                        PostCode = new RectangleCoordinates(appSettings[$"{certType}_PC"]),
                        Engineer = new RectangleCoordinates(appSettings[$"{certType}_ENG"]),
                        Supervisor = new RectangleCoordinates(appSettings[$"{certType}_SUP"]),
                        Occupier = new RectangleCoordinates(appSettings[$"{certType}_OCC"])
                    };
                }
                catch (Exception ex)
                {
                    throw new ConfigurationException($"Failed to load rectangle data for {certType}", ex);
                }
            }

            return result;
        }

        private static string GetRequiredSetting(NameValueCollection appSettings, string key)
        {
            var value = appSettings[key];
            if (string.IsNullOrEmpty(value))
                throw new ConfigurationException($"Required setting '{key}' is missing or empty");
            return value;
        }

        private static bool IsYes(string? value)
        {
            return string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static string ValidateEmailAddress(string? email, string type)
        {
            if (string.IsNullOrEmpty(email))
                throw new ConfigurationException($"{type} email address is required");

            if (!email.Contains('@') || !email.Contains('.'))
                throw new ConfigurationException($"Invalid {type} email address format: {email}");

            return email.ToLower();
        }
    }

    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message) : base(message) { }
        public ConfigurationException(string message, Exception innerException)
            : base(message, innerException) { }
    }

}
