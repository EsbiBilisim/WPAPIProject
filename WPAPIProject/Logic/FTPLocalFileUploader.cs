using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace WPAPIProject.Logic
{
    public class FTPLocalFileUploader
    {
        public string UploadToServer(byte[] file, string fileName, string fileExtension, int firmId)
        {
            string root = @"ftp://10.0.54.32:21/";
            //string root = @"ftp://185.252.41.50/";
            string filePath = $"{ToUrlSlug(fileName)}{fileExtension}";
            string filePathUpdated = root + $"/WPAPIProject/{firmId}/" + filePath;

            CreateFtpDirectory(root, $"WPAPIProject/{firmId}");

            // Dosyayı yükle
            try
            {
                var request = (FtpWebRequest)WebRequest.Create(filePathUpdated);
                request.Method = WebRequestMethods.Ftp.UploadFile;
                request.Credentials = new NetworkCredential("ftpuser", "Esbi1234!!");

                using (var stream = request.GetRequestStream())
                {
                    stream.Write(file, 0, file.Length);
                }

                using (var response = (FtpWebResponse)request.GetResponse())
                {
                    Console.WriteLine($"Upload File Complete, status {response.StatusDescription}");
                }
            }
            catch (Exception)
            {
                return "0";
            }

            return "https://sakaryaservis.com/Uploads/" + $"/WPAPIProject/{firmId}/" + filePath;
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