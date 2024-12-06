using System;

namespace WPAPIProject.Models
{
    public class W_FIRMS
    {
        public int ID { get; set; }
        public DateTime OLUSTURMATARIHI { get; set; }
        public string FIRMAUNVANI { get; set; }
        public string YETKILIADISOYADI { get; set; }
        public string KULLANICISIFRESI { get; set; }
        public string APITELEFONNO { get; set; }
        public string YETKILITELEFONNO { get; set; }
        public string WAPIKEY { get; set; }
        public int GUVENLIKKODU { get; set; }
    }
}