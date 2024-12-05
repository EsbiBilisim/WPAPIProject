using System.Text.RegularExpressions;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using static System.Net.Mime.MediaTypeNames;
using System.Net;
using System;
using Image = System.Drawing.Image;

namespace WPAPIProject.Logic
{
    public class FTPLocalImageUploader
    {
        public Image ByteArrayToImage(byte[] byteArrayIn)
        {
            var converter = new ImageConverter();
            var img = (Image)converter.ConvertFrom(byteArrayIn);
            return img;
        }

        public string UploadToServer(byte[] file, string fileName, int firmId)
        {
            string root = @"ftp://10.0.54.32:21/";
            string filePath = $"{ToUrlSlug($"{fileName}")}.{ImageFormat.Jpeg}";
            string filePathUpdated = root + "/WPAPIProject/" + firmId + "/" + filePath;

            // İlk olarak 'WPAPIProject' klasörünü oluştur
            var createDirectoryRequest = (FtpWebRequest)WebRequest.Create(root + "WPAPIProject");
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
                    return ex.Message;
                }
            }

            // Ardından 'WPAPIProject/FIRMAID' klasörünü oluştur
            createDirectoryRequest = (FtpWebRequest)WebRequest.Create(root + "WPAPIProject" + firmId);
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
                    return ex.Message;
                }
            }

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
            catch (Exception ex)
            {
                return ex.Message;
            }

            return "https://sakaryaservis.com/Uploads/" + "/WPAPIProject/" + firmId + "/" + filePath;
        }

        protected string ToUrlSlug(string value)
        {
            //First to lower case 
            value = value.ToLowerInvariant();
            //Remove all accents
            var bytes = Encoding.GetEncoding("Cyrillic").GetBytes(value);
            value = Encoding.ASCII.GetString(bytes);
            //Replace spaces 
            value = Regex.Replace(value, @"\s", "-", RegexOptions.Compiled);
            //Remove invalid chars 
            value = Regex.Replace(value, @"[^\w\s\p{Pd}]", "", RegexOptions.Compiled);
            //Trim dashes from end 
            value = value.Trim('-', '_');
            //Replace double occurences of - or \_ 
            value = Regex.Replace(value, @"([-_]){2,}", "$1", RegexOptions.Compiled);
            return value;
        }
    }
}