using System;

namespace WPAPIProject.Models
{
    public class W_USERS
    {
        public int ID { get; set; }
        public DateTime KAYITTARIHI { get; set; }
        public int FIRMID { get; set; }
        public string KULLANICIADI_USER { get; set; }
        public string KULLANICISIFRESI_USER { get; set; }
        public int GUVENLIKKODU_USER { get; set; }
        public int? DOGRULAMAKODU { get; set; }
        public DateTime? DOGRULAMAKODUZAMANASIMI { get; set; }
        public string TELEFONNO_USER { get; set; }
    }
}