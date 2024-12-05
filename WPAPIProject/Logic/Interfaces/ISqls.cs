using System.Collections.Generic;
using WPAPIProject.Models;

namespace WPAPIProject.Logic.Interfaces
{
    public interface ISqls
    {
        public void SessionSet(string key, object data);
        public object SessionGet(string key, object data);
        public List<W_USERS> tumKullanicilargetir();
        public W_USERS KullaniciGetirAd(int id);
        public W_USERS KullaniciGetir(string kullanici);
    }
}