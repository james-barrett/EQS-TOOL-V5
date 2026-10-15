using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Content;

namespace EQS_Tool
{
    internal class DevTools
    {

        public static void PrintCoordsToConsole(Page page)
        {
            if (page == null)
            {
                Console.WriteLine("Page is null.");
                return;
            }

            var words = page.GetWords();

            if (words == null || !words.Any())
            {
                Console.WriteLine("No words found on the page.");
                return;
            }

            foreach (var word in words)
            {
                if (word?.BoundingBox == null)
                {
                    Console.WriteLine("Word or BoundingBox is null.");
                    continue;
                }

                Console.WriteLine($"Text: {word.Text}, " +
                   $"Coordinates: ({word.BoundingBox.BottomLeft.X}, " +
                   $"{word.BoundingBox.BottomLeft.Y}) - ({word.BoundingBox.TopRight.X}, {word.BoundingBox.TopRight.Y})");
            }
        }

        public static void DrawRectsToPDF(string file, string deptFolderPath, string certificateType, ApplicationConfig appConfig, string timestamp)
        {
            using (var document = PdfDocument.Open(file))
            {
                var builder = new PdfDocumentBuilder { };
                var pageBuilder = builder.AddPage(document, 1);
                pageBuilder.SetStrokeColor(255, 0, 0);



                pageBuilder.DrawRectangle(
                    (new PdfPoint(appConfig.CertificateData[certificateType].Job.Left, appConfig.CertificateData[certificateType].Job.Bottom)),
                    (appConfig.CertificateData[certificateType].Job.Right - appConfig.CertificateData[certificateType].Job.Left),
                    (appConfig.CertificateData[certificateType].Job.Top - appConfig.CertificateData[certificateType].Job.Bottom)
                );

                pageBuilder.DrawRectangle(
                    (new PdfPoint(appConfig.CertificateData[certificateType].UPRN.Left, appConfig.CertificateData[certificateType].UPRN.Bottom)),
                    (appConfig.CertificateData[certificateType].UPRN.Right - appConfig.CertificateData[certificateType].UPRN.Left),
                    (appConfig.CertificateData[certificateType].UPRN.Top - appConfig.CertificateData[certificateType].UPRN.Bottom)
                );

                pageBuilder.DrawRectangle(
                    (new PdfPoint(appConfig.CertificateData[certificateType].Certificate.Left, appConfig.CertificateData[certificateType].Certificate.Bottom)),
                    (appConfig.CertificateData[certificateType].Certificate.Right - appConfig.CertificateData[certificateType].Certificate.Left),
                    (appConfig.CertificateData[certificateType].Certificate.Top - appConfig.CertificateData[certificateType].Certificate.Bottom)
                );

                pageBuilder.DrawRectangle(
                    (new PdfPoint(appConfig.CertificateData[certificateType].Date.Left, appConfig.CertificateData[certificateType].Date.Bottom)),
                    (appConfig.CertificateData[certificateType].Date.Right - appConfig.CertificateData[certificateType].Date.Left),
                    (appConfig.CertificateData[certificateType].Date.Top - appConfig.CertificateData[certificateType].Date.Bottom)
                );

                pageBuilder.DrawRectangle(
                    (new PdfPoint(appConfig.CertificateData[certificateType].Address1.Left, appConfig.CertificateData[certificateType].Address1.Bottom)),
                    (appConfig.CertificateData[certificateType].Address1.Right - appConfig.CertificateData[certificateType].Address1.Left),
                    (appConfig.CertificateData[certificateType].Address1.Top - appConfig.CertificateData[certificateType].Address1.Bottom)
                );

                pageBuilder.DrawRectangle(
                    (new PdfPoint(appConfig.CertificateData[certificateType].Address2.Left, appConfig.CertificateData[certificateType].Address2.Bottom)),
                    (appConfig.CertificateData[certificateType].Address2.Right - appConfig.CertificateData[certificateType].Address2.Left),
                    (appConfig.CertificateData[certificateType].Address2.Top - appConfig.CertificateData[certificateType].Address2.Bottom)
                );

                pageBuilder.DrawRectangle(
                    (new PdfPoint(appConfig.CertificateData[certificateType].PostCode.Left, appConfig.CertificateData[certificateType].PostCode.Bottom)),
                    (appConfig.CertificateData[certificateType].PostCode.Right - appConfig.CertificateData[certificateType].PostCode.Left),
                    (appConfig.CertificateData[certificateType].PostCode.Top - appConfig.CertificateData[certificateType].PostCode.Bottom)
                );

                pageBuilder.DrawRectangle(
                    (new PdfPoint(appConfig.CertificateData[certificateType].Engineer.Left, appConfig.CertificateData[certificateType].Engineer.Bottom)),
                    (appConfig.CertificateData[certificateType].Engineer.Right - appConfig.CertificateData[certificateType].Engineer.Left),
                    (appConfig.CertificateData[certificateType].Engineer.Top - appConfig.CertificateData[certificateType].Engineer.Bottom)
                );

                pageBuilder.DrawRectangle(
                    (new PdfPoint(appConfig.CertificateData[certificateType].Supervisor.Left, appConfig.CertificateData[certificateType].Supervisor.Bottom)),
                    (appConfig.CertificateData[certificateType].Supervisor.Right - appConfig.CertificateData[certificateType].Supervisor.Left),
                    (appConfig.CertificateData[certificateType].Supervisor.Top - appConfig.CertificateData[certificateType].Supervisor.Bottom)
                );

                pageBuilder.DrawRectangle(
                    (new PdfPoint(appConfig.CertificateData[certificateType].Occupier.Left, appConfig.CertificateData[certificateType].Occupier.Bottom)),
                    (appConfig.CertificateData[certificateType].Occupier.Right - appConfig.CertificateData[certificateType].Occupier.Left),
                    (appConfig.CertificateData[certificateType].Occupier.Top - appConfig.CertificateData[certificateType].Occupier.Bottom)
                );

                byte[] fileBytes = builder.Build();
                // written to a sub folder so the overlay is not picked up as a certificate on the next run
                string overlayDir = Path.Combine(deptFolderPath, "_OVERLAYS");
                Directory.CreateDirectory(overlayDir);
                File.WriteAllBytes(Path.Combine(overlayDir, $"rect_overlay_{timestamp}_{Path.GetFileName(file)}"), fileBytes);
            }
        }

    }
}
