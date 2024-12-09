using System;

namespace WPAPIProject.Models
{
    public class W_MESSAGES
    {
        public int ID { get; set; }
        public DateTime MESAJTARIHI { get; set; }
        public int CUSTOMERID { get; set; }
        public string ATILANMESAJ { get; set; }
        public string ATILANMESAJURL { get; set; }
        public string CUSTOMERNAME { get; set; }
    }
}