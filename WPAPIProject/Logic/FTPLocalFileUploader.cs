using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WPAPIProject.Logic
{
    public class FTPLocalFileUploader
    {
        public async Task<string> UploadToServerAsync(byte[] file, string fileName, string fileExtension, int firmId)
        {
            string root = @"ftp://10.0.54.32:21/";

            //string root = @"ftp://185.252.41.50/";

            string filePath = $"{ToUrlSlug(fileName)}{fileExtension}";
            string filePathUpdated = root + $"/WPAPIProject/{firmId}/" + filePath;

            CreateFtpDirectory(root, $"WPAPIProject/{firmId}");

            const int maxRetries = 3;
            const int delayBetweenRetriesMs = 5000;

            int attempt = 0;
            while (attempt < maxRetries)
            {
                try
                {
                    attempt++;

                    var request = (FtpWebRequest)WebRequest.Create(filePathUpdated);
                    request.Method = WebRequestMethods.Ftp.UploadFile;
                    request.Credentials = new NetworkCredential("ftpuser", "Esbi1234!!");

                    using (var stream = request.GetRequestStream())
                    {
                        await stream.WriteAsync(file, 0, file.Length);
                    }

                    using (var response = (FtpWebResponse)await request.GetResponseAsync())
                    {
                        Console.WriteLine($"Upload File Complete, status {response.StatusDescription}");
                    }

                    return "https://sakaryaservis.com/Uploads/" + $"/WPAPIProject/{firmId}/" + filePath;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Attempt {attempt} failed: {ex.Message}");

                    if (attempt >= maxRetries)
                    {
                        return "0";
                    }

                    await Task.Delay(delayBetweenRetriesMs);
                }
            }

            return "0"; 
        }

        private void CreateFtpDirectory(string root, string directoryPath)
        {
            var createDirectoryRequest = (FtpWebRequest)WebRequest.Create(root + directoryPath);
            createDirectoryRequest.Method = WebRequestMethods.Ftp.MakeDirectory;
            createDirectoryRequest.UsePassive = false;
            createDirectoryRequest.Credentials = new NetworkCredential("ftpuser", "Esbi1234!!");

            try
            {
                using (var response = (FtpWebResponse)createDirectoryRequest.GetResponse())
                {
                    Console.WriteLine($"Directory Created, status {response.StatusDescription}");
                }
            }
            catch (WebException ex)
            {
                FtpWebResponse response = (FtpWebResponse)ex.Response;
                if (response.StatusCode != FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    throw;
                }
            }
        }
        protected string ToUrlSlug(string value)
        {
            value = value.ToLowerInvariant();
            var bytes = Encoding.GetEncoding("Cyrillic").GetBytes(value);
            value = Encoding.ASCII.GetString(bytes);
            value = Regex.Replace(value, @"\s", "-", RegexOptions.Compiled);
            value = Regex.Replace(value, @"[^\w\s\p{Pd}]", "", RegexOptions.Compiled);
            value = value.Trim('-', '_');
            value = Regex.Replace(value, @"([-_]){2,}", "$1", RegexOptions.Compiled);
            return value;
        }
    }
}