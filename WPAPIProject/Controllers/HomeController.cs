using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using OfficeOpenXml;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WPAPIProject.Logic;
using WPAPIProject.Logic.Interfaces;
using WPAPIProject.Models;
using LicenseContext = OfficeOpenXml.LicenseContext;

namespace WPAPIProject.Controllers
{
    public class HomeController : BaseController
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _configuration;

        public HomeController(ISqls sql, AppDbContext db, IConfiguration configuration) : base(sql)
        {
            _db = db;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }

        public IActionResult Exit()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Home");
        }

        public bool CreateTables(int firmaId)
        {
            try
            {
                string customersTable = $"CREATE TABLE W_CUSTOMERS_{firmaId} (ID INT IDENTITY(1,1) NOT NULL, EKLENMETARIHI DATETIME, ADSOYAD NVARCHAR(75), TELEFONNO NVARCHAR(50), ISGRUBU NVARCHAR(50), AKTIF bit CONSTRAINT [PK_W_CUSTOMERS_{firmaId}] PRIMARY KEY CLUSTERED ([ID] ASC)) ON [PRIMARY]";

                string messagesTable = $"CREATE TABLE W_MESSAGES_{firmaId} (ID INT IDENTITY(1,1) NOT NULL, MESAJTARIHI DATETIME, CUSTOMERID INT, ATILANMESAJ NVARCHAR(MAX), CONSTRAINT [PK_W_MESSAGES_{firmaId}] PRIMARY KEY CLUSTERED ([ID] ASC)) ON [PRIMARY]";

                _db.Database.ExecuteSqlRaw(customersTable);
                _db.Database.ExecuteSqlRaw(messagesTable);

                return true;
            }
            catch (Exception)
            {

                return false;
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult UserLogin(string FIRMAUNVANI, string YETKILIADISOYADI, string KULLANICISIFRESI, string TELEFONNO, string WAPIKEY, int GUVENLIKKODU, string KULLANICIADI, string KULLANICISIFRESI_USER, int GUVENLIKKODU_USER)
        {
            W_USERS user = new W_USERS();

            if (string.IsNullOrEmpty(KULLANICIADI))
            {
                W_FIRMS firm = new W_FIRMS();
                firm.OLUSTURMATARIHI = DateTime.Now;
                firm.FIRMAUNVANI = FIRMAUNVANI;
                firm.YETKILIADISOYADI = YETKILIADISOYADI;
                firm.KULLANICISIFRESI = KULLANICISIFRESI;
                firm.TELEFONNO = TELEFONNO;
                firm.WAPIKEY = WAPIKEY;
                firm.GUVENLIKKODU = GUVENLIKKODU;

                _db.W_FIRMS.Add(firm);
                _db.SaveChanges();

                CreateTables(firm.ID);

                user.KAYITTARIHI = DateTime.Now;
                user.FIRMID = firm.ID;
                user.KULLANICIADI = YETKILIADISOYADI;
                user.KULLANICISIFRESI_USER = KULLANICISIFRESI;
                user.GUVENLIKKODU_USER = GUVENLIKKODU;

                _db.W_USERS.Add(user);
                _db.SaveChanges();

            }
            else
            {
                user = _db.W_USERS
               .FirstOrDefault(u =>
                   u.KULLANICIADI == KULLANICIADI &&
                   u.KULLANICISIFRESI_USER == KULLANICISIFRESI_USER &&
                   u.GUVENLIKKODU_USER == GUVENLIKKODU_USER);
            }

            if (user != null)
            {
                _sql.SessionSet("Kullanici", user);

                var sonuc = new SONUC()
                {
                    DURUM = true,
                    URL = "/Home/Index"
                };

                return Json(new { Success = true, Message = "Kullanıcı girişi başarılı!", Data = sonuc });
            }
            else
            {
                var sonuc = new SONUC()
                {
                    DURUM = false,
                    URL = "Kullanıcı Adı veya Şifre Hatalı!"
                };

                return Json(new { Success = false, Message = "Geçersiz kullanıcı adı veya şifre.", Data = sonuc });
            }
        }

        [HttpGet]
        public object FirmaMusterileriGetir(DataSourceLoadOptions loadOptions)
        {
            var kullanici = _sql.KullaniciGetir("Kullanici");

            if (kullanici != null)
            {
                var sql = $"SELECT * FROM W_CUSTOMERS_{kullanici.FIRMID} WHERE AKTIF = 1";
                var data = _db.W_CUSTOMERS.FromSqlRaw(sql).ToList();

                var result = DataSourceLoader.Load(data, loadOptions);
                return new
                {
                    sonuc = true,
                    mesaj = "Başarılı",
                    data = result
                };
            }

            return new
            {
                sonuc = false,
                mesaj = "Kullanıcı bulunamadı"
            };
        }

        [HttpGet]
        public IActionResult MesajlariGetir()
        {
            var kullanici = _sql.KullaniciGetir("Kullanici");

            if (kullanici != null)
            {
                var sql = $"SELECT * FROM W_MESSAGES_{kullanici.FIRMID}";
                var data = _db.W_MESSAGES.FromSqlRaw(sql).ToList();

                return Json(new { success = true, messages = data });
            }

            return Json(new { success = false, message = "Kullanıcı bulunamadı." });
        }

        private string FormatPhoneNumber(string phoneNumber)
        {
            phoneNumber = phoneNumber
                .Replace("(", "")
                .Replace(")", "")
                .Replace(" ", "")
                .Replace("-", "");

            if (!phoneNumber.StartsWith("90"))
            {
                if (phoneNumber.StartsWith("9"))
                {
                    phoneNumber = "90" + phoneNumber.Substring(1);
                }
                else if (phoneNumber.StartsWith("0"))
                {
                    phoneNumber = "90" + phoneNumber.Substring(1);
                }
                else
                {
                    phoneNumber = "90" + phoneNumber;
                }
            }

            if (phoneNumber.Length != 12)
            {
                throw new Exception("Telefon numarası 90XXXXXXXXXX formatında olmalıdır.");
            }

            return phoneNumber;
        }

        [HttpPost]
        public IActionResult Upload(IFormFile file, [FromServices] IConfiguration configuration)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { sonuc = false, mesaj = "Lütfen bir Excel dosyası yükleyin." });
            }

            try
            {
                var kullanici = _sql.KullaniciGetir("Kullanici");

                DataTable dataTable;

                using (var stream = file.OpenReadStream())
                {
                    dataTable = ReadExcelFile(stream);
                }

                string connectionString = configuration.GetConnectionString("AppDbContext");

                SaveToDatabase(dataTable, connectionString, kullanici);

                return Ok(new { sonuc = true, mesaj = "Veriler başarıyla yüklendi ve kaydedildi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { sonuc = false, mesaj = $"Bir hata oluştu: {ex.Message}" });
            }
        }

        private DataTable ReadExcelFile(Stream fileStream)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            DataTable dataTable = new DataTable();

            using (ExcelPackage package = new ExcelPackage(fileStream))
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                if (worksheet == null)
                {
                    throw new Exception("Excel sayfası bulunamadı.");
                }

                var columnMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "AD SOYAD", "ADSOYAD" },
            { "ADI SOYADI", "ADSOYAD" },
            { "ADSOYAD", "ADSOYAD" },
            { "İSİM", "ADSOYAD" },
            { "TELEFONNO", "TELEFONNO" },
            { "TELEFON NO", "TELEFONNO" },
            { "TEL NO", "TELEFONNO" },
            { "TEL", "TELEFONNO" },
            { "İŞ GRUBU", "ISGRUBU" },
            { "İS GRUBU", "ISGRUBU" },
            { "İSGRUBU", "ISGRUBU" }
        };

                int headerRow = 1;
                Dictionary<string, int> columnIndexes = new Dictionary<string, int>();

                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    string headerValue = worksheet.Cells[headerRow, col].Text.Trim().ToUpper();

                    if (columnMappings.TryGetValue(headerValue, out string mappedHeader))
                    {
                        columnIndexes[mappedHeader] = col;
                        dataTable.Columns.Add(mappedHeader, typeof(string));
                    }
                }

                var requiredHeaders = new[] { "ADSOYAD", "TELEFONNO", "ISGRUBU" };
                var missingHeaders = requiredHeaders.Except(columnIndexes.Keys).ToList();
                if (missingHeaders.Any())
                {
                    throw new Exception($"Eksik sütun başlıkları: {string.Join(", ", missingHeaders)}");
                }

                for (int row = headerRow + 1; row <= worksheet.Dimension.End.Row; row++)
                {
                    DataRow newRow = dataTable.NewRow();

                    foreach (var header in columnIndexes)
                    {
                        string cellValue = worksheet.Cells[row, header.Value].Text.Trim();
                        newRow[header.Key] = header.Key == "TELEFONNO"
                            ? FormatPhoneNumber(cellValue)
                            : cellValue;
                    }

                    dataTable.Rows.Add(newRow);
                }
            }

            return dataTable;
        }

        private void SaveToDatabase(DataTable dataTable, string connectionString, object kullanici)
        {
            var firmId = kullanici as W_USERS;
            string tableName = $"W_CUSTOMERS_{firmId.FIRMID}";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                foreach (DataRow row in dataTable.Rows)
                {
                    bool isValid = true;

                    string phoneNumber = row["TELEFONNO"].ToString();
                    try
                    {
                        phoneNumber = FormatPhoneNumber(phoneNumber);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Hatalı telefon numarası: {phoneNumber}. Hata: {ex.Message}");
                        isValid = false;
                    }

                    if (!isValid)
                    {
                        continue;
                    }

                    var columnNames = new List<string>();
                    var parameterNames = new List<string>();
                    var parameters = new Dictionary<string, object>();

                    foreach (DataColumn column in dataTable.Columns)
                    {
                        string columnName = column.ColumnName.Trim();
                        string normalizedColumnName = columnName.ToUpper();

                        if (normalizedColumnName == "İŞ GRUBU" || normalizedColumnName == "İS GRUBU" || normalizedColumnName == "İSGRUBU")
                        {
                            columnName = "ISGRUBU";
                        }
                        else if (normalizedColumnName == "AD SOYAD" || normalizedColumnName == "ADSOYAD" || normalizedColumnName == "ADI SOYADI")
                        {
                            columnName = "ADSOYAD";
                        }
                        else if (normalizedColumnName == "TEL" || normalizedColumnName == "TEL NO" || normalizedColumnName == "TELEFON NO")
                        {
                            columnName = "TELEFONNO";
                        }

                        string parameterName = $"@{columnName}";

                        columnNames.Add(columnName);
                        parameterNames.Add(parameterName);
                        parameters.Add(parameterName, row[column.ColumnName]);
                    }

                    columnNames.Add("EKLENMETARIHI");
                    parameterNames.Add("@EKLENMETARIHI");
                    parameters.Add("@EKLENMETARIHI", DateTime.Now);

                    columnNames.Add("AKTIF");
                    parameterNames.Add("@AKTIF");
                    parameters.Add("@AKTIF", true);

                    string query = $@"
                            INSERT INTO {tableName} 
                            ({string.Join(", ", columnNames)}) 
                            VALUES 
                            ({string.Join(", ", parameterNames)})";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        foreach (var parameter in parameters)
                        {
                            command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
                        }

                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        [HttpPost]
        public async Task<JsonResult> MusteriWpMesajGonder()
        {
            try
            {
                var kullanici = _sql.KullaniciGetir("Kullanici");

                var aciklama = Request.Form["aciklama"].ToString();

                var selectedDataJson = Request.Form["selectedData"];
                var selectedData = JsonConvert.DeserializeObject<List<W_CUSTOMERS>>(selectedDataJson);

                string duzenlenmisAciklama = aciklama.Replace("\n", " ").Replace("\r", "");

                var firma = _db.W_FIRMS.Where(s => s.ID == kullanici.FIRMID).FirstOrDefault();

                string cleanedNumbers = string.Join(",", selectedData.Select(s => s.TELEFONNO).ToList());

                List<string> dosyaYollari = new List<string>();
                List<string> dosyaAdlari = new List<string>();

                if (Request.Form.Files.Count > 0)
                {
                    var files = Request.Form.Files;

                    for (int i = 0; i < files.Count; i++)
                    {
                        var file = files[i];

                        if (file != null && file.Length > 0)
                        {
                            var fileExtension = Path.GetExtension(file.FileName).ToLower();
                            var originalFileName = Path.GetFileNameWithoutExtension(file.FileName);
                            string fname = originalFileName + fileExtension;

                            using (Stream fs = file.OpenReadStream())
                            {
                                using (BinaryReader br = new BinaryReader(fs))
                                {
                                    byte[] bytes = br.ReadBytes((int)file.Length);

                                    FTPLocalFileUploader up = new FTPLocalFileUploader();
                                    string son = up.UploadToServer(bytes, originalFileName, fileExtension, kullanici.FIRMID);

                                    if (son != "0")
                                    {
                                        dosyaYollari.Add(son);
                                        dosyaAdlari.Add(fname);
                                    }
                                }
                            }
                        }
                    }
                }

                if (Request.Form.Files.Count == 0)
                {
                    var options = new RestClientOptions($"https://www.wapifly.com/api/{firma.TELEFONNO}/send-message");
                    var client = new RestClient(options);
                    var request = new RestRequest("");
                    request.AddHeader("accept", "application/json");
                    request.AddHeader("Accept-Language", "tr");
                    request.AddHeader("wapikey", firma.WAPIKEY);

                    var payload = new
                    {
                        type = 1,
                        interval = 1,
                        autoblacklist = false,
                        blacklistlink = false,
                        numbers = cleanedNumbers,
                        message = duzenlenmisAciklama
                    };

                    request.AddJsonBody(payload);

                    var response = await client.PostAsync<RestResponse>(request);

                    if (response != null && (response.IsSuccessful || response.StatusCode == 0))
                    {
                        Console.WriteLine($"API Yanıtı: {response.Content}");
                    }
                    else
                    {
                        Console.WriteLine($"API Hata: {response?.ErrorMessage}");
                    }
                }
                else
                {
                    if (duzenlenmisAciklama.Length <= 1024)
                    {
                        for (int i = 0; i < dosyaYollari.Count; i++)
                        {
                            var options = new RestClientOptions($"https://www.wapifly.com/api/{firma.TELEFONNO}/send-message");
                            var client = new RestClient(options);
                            var request = new RestRequest("");
                            request.AddHeader("accept", "application/json");
                            request.AddHeader("Accept-Language", "tr");
                            request.AddHeader("wapikey", firma.WAPIKEY);

                            var payload = new
                            {
                                type = 2,
                                interval = 1,
                                autoblacklist = false,
                                blacklistlink = false,
                                numbers = cleanedNumbers,
                                message = duzenlenmisAciklama,
                                url = dosyaYollari[i]
                            };

                            request.AddJsonBody(payload);

                            var response = await client.PostAsync<RestResponse>(request);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < dosyaYollari.Count; i++)
                        {
                            var optionsx = new RestClientOptions($"https://www.wapifly.com/api/{firma.TELEFONNO}/send-message");
                            var clientx = new RestClient(optionsx);
                            var requestx = new RestRequest("");
                            requestx.AddHeader("accept", "application/json");
                            requestx.AddHeader("Accept-Language", "tr");
                            requestx.AddHeader("wapikey", firma.WAPIKEY);

                            var payloadx = new
                            {
                                type = 2,
                                interval = 1,
                                autoblacklist = false,
                                blacklistlink = false,
                                numbers = cleanedNumbers,
                                message = "",
                                url = dosyaYollari[i]
                            };

                            requestx.AddJsonBody(payloadx);

                            var responsex = await clientx.PostAsync<RestResponse>(requestx);
                        }

                        var options = new RestClientOptions($"https://www.wapifly.com/api/{firma.TELEFONNO}/send-message");
                        var client = new RestClient(options);
                        var request = new RestRequest("");
                        request.AddHeader("accept", "application/json");
                        request.AddHeader("Accept-Language", "tr");
                        request.AddHeader("wapikey", firma.WAPIKEY);

                        var payload = new
                        {
                            type = 1,
                            interval = 1,
                            autoblacklist = false,
                            blacklistlink = false,
                            numbers = cleanedNumbers,
                            message = duzenlenmisAciklama
                        };

                        request.AddJsonBody(payload);

                        var response = await client.PostAsync<RestResponse>(request);
                    }
                }
                return Json(new { Sonuc = true, Msg = "İşlem Başarılı!" });
            }
            catch (Exception ex)
            {
                return Json(new { Sonuc = false, Msg = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult UpdateRecord([FromBody] List<W_CUSTOMERS> records)
        {
            try
            {
                if (records == null || !records.Any())
                {
                    return Json(new { success = false, message = "Güncellenmesi gereken kayıt yok." });
                }

                var kullanici = _sql.KullaniciGetir("Kullanici");
                string tableName = $"W_CUSTOMERS_{kullanici.FIRMID}";

                using (var connection = new SqlConnection(_configuration.GetConnectionString("AppDbContext")))
                {
                    connection.Open();

                    foreach (var record in records)
                    {
                        string query = $@"
                                UPDATE {tableName} 
                                SET 
                                    ADSOYAD = @ADSOYAD, 
                                    TELEFONNO = @TELEFONNO,
                                    ISGRUBU = @ISGRUBU
                                WHERE 
                                    ID = @ID";

                        using (var command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@ID", record.ID);
                            command.Parameters.AddWithValue("@ADSOYAD", record.ADSOYAD);
                            command.Parameters.AddWithValue("@TELEFONNO", record.TELEFONNO);
                            command.Parameters.AddWithValue("@ISGRUBU", record.ISGRUBU);

                            command.ExecuteNonQuery();
                        }
                    }
                }

                return Json(new { success = true, message = "Kayıtlar başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Hata: {ex.Message}" });
            }
        }

        [HttpPost]
        public IActionResult DeleteRecord([FromBody] List<int> IDs)
        {
            try
            {
                if (IDs == null || IDs.Count == 0)
                {
                    return Json(new { success = false, message = "Silinecek kayıt seçilmedi." });
                }

                var kullanici = _sql.KullaniciGetir("Kullanici");
                string tableName = $"W_CUSTOMERS_{kullanici.FIRMID}";

                using (var connection = new SqlConnection(_configuration.GetConnectionString("AppDbContext")))
                {
                    connection.Open();

                    string idList = string.Join(",", IDs);

                    string query = $@"
                        UPDATE {tableName}
                        SET AKTIF = 0
                        WHERE ID IN ({idList})";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true, message = "Seçilen kayıtlar başarıyla silindi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Hata: {ex.Message}" });
            }
        }

        [HttpPost]
        public IActionResult AddRecord([FromBody] W_CUSTOMERS yeniKayit)
        {
            try
            {
                if (yeniKayit == null)
                {
                    return Json(new { success = false, message = "Eklemek için geçerli bir kayıt verilmedi." });
                }

                var kullanici = _sql.KullaniciGetir("Kullanici");
                string tableName = $"W_CUSTOMERS_{kullanici.FIRMID}";

                using (var connection = new SqlConnection(_configuration.GetConnectionString("AppDbContext")))
                {
                    connection.Open();

                    string query = $@"
        INSERT INTO {tableName} (ADSOYAD, TELEFONNO, ISGRUBU, EKLENMETARIHI, AKTIF)
        VALUES (@ADSOYAD, @TELEFONNO, @ISGRUBU, @EKLENMETARIHI, @AKTIF)";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ADSOYAD", yeniKayit.ADSOYAD);
                        command.Parameters.AddWithValue("@TELEFONNO", yeniKayit.TELEFONNO);
                        command.Parameters.AddWithValue("@ISGRUBU", yeniKayit.ISGRUBU);
                        command.Parameters.AddWithValue("@EKLENMETARIHI", DateTime.Now);
                        command.Parameters.AddWithValue("@AKTIF", true);

                        command.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true, message = "Kayıt başarıyla eklendi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Hata: {ex.Message}" });
            }
        }
    }
}