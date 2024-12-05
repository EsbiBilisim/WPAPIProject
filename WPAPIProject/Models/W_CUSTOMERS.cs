using System;

namespace WPAPIProject.Models
{
    public class W_CUSTOMERS
    {
        public int ID { get; set; }
        public DateTime EKLENMETARIHI { get; set; }
        public string ADSOYAD { get; set; }
        public string TELEFONNO { get; set; }
        public string ISGRUBU { get; set; }
        public bool AKTIF { get; set; }
    }
}