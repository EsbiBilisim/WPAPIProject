﻿using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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

        public IActionResult FirmaBilgileriniDuzenle()
        {
            var user = _sql.KullaniciGetir("Kullanici");

            var firmaBilgileri = _db.W_FIRMS.Where(s => s.ID == user.ID).FirstOrDefault();

            return View(firmaBilgileri);
        }

        [HttpPost]
        public JsonResult firmaBilgiGuncelle(W_FIRMS firmaBilgileri)
        {
            try
            {
                //----------------------------------------------------------------------------
                var existingFirma = _db.W_FIRMS.FirstOrDefault(s => s.ID == firmaBilgileri.ID);

                var user = _sql.KullaniciGetir("Kullanici");
                if (user == null)
                {
                    return Json(new { Success = false, Message = "Kullanıcı oturumu bulunamadı." });
                }

                var dbUser = _db.W_USERS.FirstOrDefault(u => u.ID == user.ID);

                if (existingFirma == null)
                {
                    return Json(new { success = false, message = "Firma bulunamadı." });
                }
                //------------------------HATA KONTROLÜ----------------------------------
                var missingFields = new List<string>();

                if (string.IsNullOrEmpty(firmaBilgileri.FIRMAUNVANI))
                    missingFields.Add("Firma Ünvanı");

                if (string.IsNullOrEmpty(firmaBilgileri.YETKILIADISOYADI))
                    missingFields.Add("Yetkili Adı Soyadı");

                if (string.IsNullOrEmpty(firmaBilgileri.KULLANICISIFRESI))
                    missingFields.Add("Kullanıcı Şifresi");

                if (string.IsNullOrEmpty(firmaBilgileri.APITELEFONNO))
                    missingFields.Add("API Telefon Numarası");

                if (string.IsNullOrEmpty(firmaBilgileri.YETKILITELEFONNO))
                    missingFields.Add("Yetkili Telefon Numarası");

                if (string.IsNullOrEmpty(firmaBilgileri.WAPIKEY))
                    missingFields.Add("WAPI Key");

                if (firmaBilgileri.GUVENLIKKODU == 0)
                    missingFields.Add("Güvenlik Kodu");

                if (firmaBilgileri.ID == null)
                    missingFields.Add("ID");

                if (missingFields.Any())
                {
                    string missingFieldsMessage = string.Join(", ", missingFields);
                    return Json(new { success = false, message = $"Tüm alanların doldurulması zorunludur: {missingFieldsMessage}" });
                }

                //-----------------------FİRMA BİLGİLERİ-----------------------------------
                existingFirma.FIRMAUNVANI = firmaBilgileri.FIRMAUNVANI;
                existingFirma.YETKILIADISOYADI = firmaBilgileri.YETKILIADISOYADI;
                existingFirma.KULLANICISIFRESI = firmaBilgileri.KULLANICISIFRESI;
                existingFirma.APITELEFONNO = firmaBilgileri.APITELEFONNO;
                existingFirma.YETKILITELEFONNO = firmaBilgileri.YETKILITELEFONNO;
                existingFirma.WAPIKEY = firmaBilgileri.WAPIKEY;
                existingFirma.GUVENLIKKODU = firmaBilgileri.GUVENLIKKODU;
                //-----------------------KULLANICI BİLGİLERİ-------------------------------------
                dbUser.GUVENLIKKODU_USER = firmaBilgileri.GUVENLIKKODU;
                dbUser.KULLANICIADI_USER = firmaBilgileri.YETKILIADISOYADI;
                dbUser.KULLANICISIFRESI_USER = firmaBilgileri.KULLANICISIFRESI;
                dbUser.TELEFONNO_USER = firmaBilgileri.YETKILITELEFONNO;
                //----------------------------------------------------------------------------

                _db.SaveChanges();

                return Json(new { success = true, message = "Firma bilgileri başarıyla güncellendi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Bir hata oluştu: " + ex.Message });
            }
        }


        public IActionResult Logs()
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
                string customersTable = $"CREATE TABLE W_CUSTOMERS_{firmaId} (ID INT IDENTITY(1,1) NOT NULL, EKLENMETARIHI DATETIME, ADSOYAD NVARCHAR(50), TELEFONNO NVARCHAR(12), ISGRUBU NVARCHAR(50), AKTIF BIT CONSTRAINT [PK_W_CUSTOMERS_{firmaId}] PRIMARY KEY CLUSTERED ([ID] ASC)) ON [PRIMARY]";

                string messagesTable = $"CREATE TABLE W_MESSAGES_{firmaId} (ID INT IDENTITY(1,1) NOT NULL, MESAJTARIHI DATETIME, CUSTOMERID INT, ATILANMESAJ NVARCHAR(MAX), ATILANMESAJURL NVARCHAR(MAX) CONSTRAINT [PK_W_MESSAGES_{firmaId}] PRIMARY KEY CLUSTERED ([ID] ASC)) ON [PRIMARY]";

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
        public async Task<IActionResult> UserLogin(string FIRMAUNVANI, string YETKILIADISOYADI, string KULLANICISIFRESI, string APITELEFONNO, string YETKILITELEFONNO, string WAPIKEY, int GUVENLIKKODU, string KULLANICIADI, string KULLANICISIFRESI_USER, int GUVENLIKKODU_USER)
        {
            W_USERS user = new W_USERS();

            if (string.IsNullOrEmpty(KULLANICIADI))
            {
                W_FIRMS firm = new W_FIRMS();
                firm.OLUSTURMATARIHI = DateTime.Now;
                firm.FIRMAUNVANI = FIRMAUNVANI;
                firm.YETKILIADISOYADI = YETKILIADISOYADI;
                firm.KULLANICISIFRESI = KULLANICISIFRESI;
                firm.APITELEFONNO = APITELEFONNO;
                firm.YETKILITELEFONNO = YETKILITELEFONNO;
                firm.WAPIKEY = WAPIKEY;
                firm.GUVENLIKKODU = GUVENLIKKODU;

                _db.W_FIRMS.Add(firm);
                _db.SaveChanges();

                CreateTables(firm.ID);

                user.KAYITTARIHI = DateTime.Now;
                user.FIRMID = firm.ID;
                user.KULLANICIADI_USER = YETKILIADISOYADI;
                user.KULLANICISIFRESI_USER = KULLANICISIFRESI;
                user.GUVENLIKKODU_USER = GUVENLIKKODU;
                user.TELEFONNO_USER = YETKILITELEFONNO;

                _db.W_USERS.Add(user);
                _db.SaveChanges();

            }
            else
            {
                user = _db.W_USERS
               .FirstOrDefault(u =>
                   u.KULLANICIADI_USER == KULLANICIADI &&
                   u.KULLANICISIFRESI_USER == KULLANICISIFRESI_USER &&
                   u.GUVENLIKKODU_USER == GUVENLIKKODU_USER);
            }

            if (user != null)
            {
                _sql.SessionSet("Kullanici", user);

                Random random = new Random();
                int verificationCode = random.Next(10000, 99999);

                var options = new RestClientOptions("https://www.wapifly.com/api/902642780280/send-message");
                var client = new RestClient(options);
                var request = new RestRequest("");
                request.AddHeader("accept", "application/json");
                request.AddHeader("Accept-Language", "tr");
                request.AddHeader("wapikey", "39706fd7d8a2d870b7f67ab335f0a8cf395878664439f7aaa4f26cd7e647f80e");

                var firmaYetkilisi = _db.W_FIRMS.Where(s => s.ID == user.FIRMID).FirstOrDefault();

                var payload = new
                {
                    type = 1,
                    interval = 1,
                    autoblacklist = false,
                    blacklistlink = false,
                    numbers = firmaYetkilisi.YETKILITELEFONNO,
                    message = "Doğrulama Kodunuz: " + verificationCode.ToString()
                };

                request.AddJsonBody(payload);

                var response = await client.PostAsync<RestResponse>(request);

                if (response != null && (response.IsSuccessful || response.StatusCode == 0))
                {
                    Console.WriteLine($"API Yanıtı: {response.Content}");

                    user.DOGRULAMAKODU = verificationCode;
                    user.DOGRULAMAKODUZAMANASIMI = DateTime.Now.AddMinutes(5);
                    _db.SaveChanges();
                }
                else
                {
                    Console.WriteLine($"API Hata: {response?.ErrorMessage}");
                }

                return Json(new { Success = true, Message = "Kullanıcı girişi başarılı!" });
            }
            else
            {
                return Json(new { Success = false, Message = "Geçersiz kullanıcı adı veya şifre." });
            }
        }

        [HttpPost]
        public IActionResult VerifyCode(int verificationCode)
        {
            var user = _sql.KullaniciGetir("Kullanici");
            if (user == null)
            {
                return Json(new { Success = false, Message = "Kullanıcı oturumu bulunamadı." });
            }

            var dbUser = _db.W_USERS.FirstOrDefault(u => u.ID == user.ID);

            if (dbUser != null &&
                dbUser.DOGRULAMAKODU == verificationCode &&
                dbUser.DOGRULAMAKODUZAMANASIMI > DateTime.Now)
            {
                dbUser.DOGRULAMAKODU = null;
                dbUser.DOGRULAMAKODUZAMANASIMI = null;
                _db.SaveChanges();

                var result = new SONUC
                {
                    DURUM = true,
                    URL = "/Home/Index"
                };

                return Json(new { Success = true, Message = "Doğrulama başarılı!", Data = result });
            }

            HttpContext.Session.Clear();
            return Json(new { Success = false, Message = "Doğrulama kodu hatalı veya süresi dolmuş." });
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
        public object FirmaMesajLogKayitlari(DataSourceLoadOptions loadOptions)
        {
            var kullanici = _sql.KullaniciGetir("Kullanici");

            if (kullanici != null)
            {
                var sql = $@"
            SELECT 
                M.*, 
                C.ADSOYAD AS CUSTOMERNAME 
            FROM 
                W_MESSAGES_{kullanici.FIRMID} M
            LEFT OUTER JOIN 
                W_CUSTOMERS_{kullanici.FIRMID} C 
            ON 
                M.CUSTOMERID = C.ID
            ORDER BY ID DESC";

                var data = _db.W_MESSAGES.FromSqlRaw(sql).ToList();

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

        [HttpPost]
        public IActionResult MesajLogKayitlariDosyalar(int rowId)
        {
            var kullanici = _sql.KullaniciGetir("Kullanici");

            if (kullanici != null)
            {
                var tableName = $"W_MESSAGES_{kullanici.FIRMID}";

                using (var connection = new SqlConnection(_configuration.GetConnectionString("AppDbContext")))
                {
                    connection.Open();

                    var query = $"SELECT ATILANMESAJURL FROM {tableName} WHERE ID = @RowId";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@RowId", rowId);

                        var result = command.ExecuteScalar() as string;
                        if (!string.IsNullOrEmpty(result))
                        {
                            var urls = result.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                            return Json(new
                            {
                                success = true,
                                message = "Dosyalar başarıyla yüklendi.",
                                urls
                            });
                        }
                        else
                        {
                            return Json(new
                            {
                                success = false,
                                message = "Kayıt bulunamadı veya dosya URL'si boş."
                            });
                        }
                    }
                }
            }

            return Json(new
            {
                success = false,
                message = "Kullanıcı bilgisi alınamadı."
            });
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

            return phoneNumber;
        }

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, [FromServices] IConfiguration configuration, [FromServices] IHubContext<ProgressHub> hubContext)
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

                await SaveToDatabase(dataTable, connectionString, kullanici, hubContext);

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
                                                { "İSGRUBU", "ISGRUBU" },
                                                { "ISGRUBU", "ISGRUBU" }
                                            };

                foreach (var worksheet in package.Workbook.Worksheets)
                {
                    if (worksheet == null || worksheet.Dimension == null)
                        continue;

                    Dictionary<string, int> columnIndexes = GetColumnIndexes(worksheet, columnMappings);

                    var requiredHeaders = new[] { "ADSOYAD", "TELEFONNO", "ISGRUBU" };
                    if (!requiredHeaders.All(columnIndexes.ContainsKey))
                        continue;

                    if (dataTable.Columns.Count == 0)
                    {
                        foreach (var column in columnIndexes.Keys)
                        {
                            dataTable.Columns.Add(column, typeof(string));
                        }
                    }

                    int headerRow = 1;
                    for (int row = headerRow + 1; row <= worksheet.Dimension.End.Row; row++)
                    {
                        DataRow newRow = dataTable.NewRow();
                        bool hasData = false;

                        foreach (var column in columnIndexes)
                        {
                            string cellValue = worksheet.Cells[row, column.Value].Text.Trim();
                            if (!string.IsNullOrEmpty(cellValue))
                                hasData = true;

                            newRow[column.Key] = column.Key == "TELEFONNO"
                                ? FormatPhoneNumber(cellValue)
                                : cellValue;
                        }

                        if (hasData)
                            dataTable.Rows.Add(newRow);
                    }
                }
            }

            return dataTable;
        }

        private Dictionary<string, int> GetColumnIndexes(ExcelWorksheet worksheet, Dictionary<string, string> columnMappings)
        {
            int headerRow = 1;
            Dictionary<string, int> columnIndexes = new Dictionary<string, int>();

            for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
            {
                string headerValue = worksheet.Cells[headerRow, col].Text.Trim();
                if (columnMappings.TryGetValue(headerValue, out string mappedHeader))
                {
                    columnIndexes[mappedHeader] = col;
                }
            }

            return columnIndexes;
        }

        private async Task SaveToDatabase(DataTable dataTable, string connectionString, object kullanici, IHubContext<ProgressHub> hubContext)
        {
            var firmId = kullanici as W_USERS;
            string tableName = $"W_CUSTOMERS_{firmId.FIRMID}";

            int totalRows = dataTable.Rows.Count;
            int processedRows = 0;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                foreach (DataRow row in dataTable.Rows)
                {
                    processedRows++;

                    bool isValid = true;
                    string phoneNumber = row["TELEFONNO"].ToString();
                    try
                    {
                        phoneNumber = FormatPhoneNumber(phoneNumber);
                    }
                    catch
                    {
                        isValid = false;
                    }

                    if (!isValid) continue;

                    string checkQuery = $@"SELECT COUNT(*) FROM {tableName} WHERE TELEFONNO = @TELEFONNO";
                    using (SqlCommand checkCommand = new(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@TELEFONNO", phoneNumber);
                        int count = (int)checkCommand.ExecuteScalar();
                        if (count > 0) continue;
                    }

                    var columnNames = new List<string> { "TELEFONNO", "ADSOYAD", "ISGRUBU", "EKLENMETARIHI", "AKTIF" };
                    var parameterNames = new List<string> { "@TELEFONNO", "@ADSOYAD", "@ISGRUBU", "@EKLENMETARIHI", "@AKTIF" };
                    var parameters = new Dictionary<string, object>
            {
                { "@TELEFONNO", row["TELEFONNO"] },
                { "@ADSOYAD", row["ADSOYAD"] },
                { "@ISGRUBU", row["ISGRUBU"] },
                { "@EKLENMETARIHI", DateTime.Now },
                { "@AKTIF", true }
            };

                    string query = $@"
                INSERT INTO {tableName} 
                ({string.Join(", ", columnNames)}) 
                VALUES 
                ({string.Join(", ", parameterNames)})";

                    using (SqlCommand command = new(query, connection))
                    {
                        foreach (var parameter in parameters)
                        {
                            command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
                        }

                        await command.ExecuteNonQueryAsync();
                    }

                    int progress = (processedRows * 100) / totalRows;
                    await hubContext.Clients.All.SendAsync("ReceiveProgress", $"İşleniyor: {processedRows}/{totalRows}", progress);
                }
            }
        }

        public async Task<JsonResult> UploadFilesAndReturnPaths()
        {
            var kullanici = _sql.KullaniciGetir("Kullanici");

            List<string> dosyaYollari = new List<string>();

            if (Request.Form.Files.Count > 0)
            {
                var files = Request.Form.Files;

                foreach (var file in files)
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
                            string result = await up.UploadToServerAsync(bytes, originalFileName, fileExtension, kullanici.FIRMID);

                            if (result != "0")
                            {
                                dosyaYollari.Add(result);
                            }
                        }
                    }
                }
            }

            return Json(new { success = true, dosyaYollari = dosyaYollari });
        }

        [HttpPost]
        public async Task<JsonResult> MusteriWpMesajGonder(string aciklama, List<W_CUSTOMERS> selectedData, List<string> dosyaYollari)
        {
            try
            {
                var kullanici = _sql.KullaniciGetir("Kullanici");

                string tableName = $"W_MESSAGES_{kullanici.FIRMID}";

                var firma = _db.W_FIRMS.Where(s => s.ID == kullanici.FIRMID).FirstOrDefault();
                string baseUrl = "https://www.esbi.com.tr/logobuluterplite/logobuluterplite_basvuru.php";

                foreach (var customer in selectedData)
                {
                    string kisiyeOzelMesaj = aciklama
                        .Replace("@AdSoyad", customer.ADSOYAD ?? "")
                        .Replace("@Telefon", customer.TELEFONNO ?? "")
                        .Replace("@İşGrubu", customer.ISGRUBU ?? "");

                    if (aciklama.Contains(baseUrl))
                    {
                        string personalizedUrl = $"{baseUrl}?basvuruYetkili={Uri.EscapeDataString(customer.ADSOYAD)}%20{Uri.EscapeDataString(customer.TELEFONNO)}";
                        kisiyeOzelMesaj = kisiyeOzelMesaj.Replace(baseUrl, personalizedUrl);
                    }

                    await GonderimIslemi(customer.ID,customer.TELEFONNO, kisiyeOzelMesaj, dosyaYollari, tableName, firma);
                }

                return Json(new { Sonuc = true, Msg = "İşlem Başarılı!" });
            }
            catch (Exception ex)
            {
                return Json(new { Sonuc = false, Msg = ex.Message });
            }
        }

        private async Task GonderimIslemi(int customerId, string telefonNo, string mesaj, List<string> dosyaYollari, string tableName, W_FIRMS firma)
        {
            int maxRetry = 3;
            int retryCount = 0;
            bool success = false;

            while (retryCount < maxRetry && !success)
            {
                try
                {
                    if (dosyaYollari.Count == 0)
                    {
                        success = await TekliMesajGonder(customerId, telefonNo, mesaj, tableName, firma);
                    }
                    else
                    {
                        success = await DosyaMesajGonder(customerId, telefonNo, mesaj, dosyaYollari, tableName, firma);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Hata: {ex.Message}, Tekrar Deneme: {retryCount + 1}");
                }

                if (!success)
                {
                    retryCount++;
                    await Task.Delay(5000);
                }
            }

            if (!success)
            {
                throw new Exception("Mesaj gönderimi başarısız oldu.");
            }
        }

        private async Task<bool> TekliMesajGonder(int customerId, string telefonNo, string mesaj, string tableName, W_FIRMS firma)
        {
            var options = new RestClientOptions($"https://www.wapifly.com/api/{firma.APITELEFONNO}/send-message");
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
                numbers = telefonNo,
                message = mesaj
            };

            request.AddJsonBody(payload);
            var response = await client.PostAsync<RestResponse>(request);

            if (response != null && (response.IsSuccessful || response.StatusCode == 0))
            {
                await SqlKayit(customerId, tableName, telefonNo, mesaj, "");
                return true;
            }

            return false;
        }

        private async Task<bool> DosyaMesajGonder(int customerId, string telefonNo, string mesaj, List<string> dosyaYollari, string tableName, W_FIRMS firma)
        {
            foreach (var dosyaYolu in dosyaYollari)
            {
                var options = new RestClientOptions($"https://www.wapifly.com/api/{firma.APITELEFONNO}/send-message");
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
                    numbers = telefonNo,
                    message = mesaj.Length <= 1024 ? mesaj : "",
                    url = dosyaYolu
                };

                request.AddJsonBody(payload);
                var response = await client.PostAsync<RestResponse>(request);

                if (response == null || (!response.IsSuccessful && response.StatusCode != 0))
                {
                    return false;
                }
            }

            await SqlKayit(customerId, tableName, telefonNo, mesaj, string.Join(",", dosyaYollari));
            return true;
        }

        private async Task SqlKayit(int customerId, string tableName, string telefonNo, string mesaj, string dosyaUrl)
        {
            try
            {
                using (var connection = new SqlConnection(_configuration.GetConnectionString("AppDbContext")))
                {
                    connection.Open();

                    string query = $@"
            INSERT INTO {tableName} (MESAJTARIHI, CUSTOMERID, ATILANMESAJ, ATILANMESAJURL)
            VALUES (@MESAJTARIHI, @CUSTOMERID, @ATILANMESAJ, @ATILANMESAJURL)";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@MESAJTARIHI", DateTime.Now);
                        command.Parameters.AddWithValue("@CUSTOMERID", customerId);
                        command.Parameters.AddWithValue("@ATILANMESAJ", mesaj);
                        command.Parameters.AddWithValue("@ATILANMESAJURL", dosyaUrl);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception)
            {

                throw;
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
        public IActionResult AddRecord([FromBody] List<W_CUSTOMERS> yeniKayitlar)
        {
            try
            {
                if (yeniKayitlar == null || yeniKayitlar.Count == 0)
                {
                    return Json(new { success = false, message = "Eklemek için geçerli kayıt verilmedi." });
                }

                var kullanici = _sql.KullaniciGetir("Kullanici");
                string tableName = $"W_CUSTOMERS_{kullanici.FIRMID}";

                using (var connection = new SqlConnection(_configuration.GetConnectionString("AppDbContext")))
                {
                    connection.Open();

                    foreach (var yeniKayit in yeniKayitlar)
                    {
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
                }

                return Json(new { success = true, message = "Kayıtlar başarıyla eklendi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Hata: {ex.Message}" });
            }
        }
    }
}