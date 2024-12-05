using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Linq;
using WPAPIProject.Extensions;
using WPAPIProject.Logic.Interfaces;
using WPAPIProject.Models;

namespace WPAPIProject.Logic.Repository
{
    public class Sqls : ISqls
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _db;
        private readonly IMemoryCache _mcache;
        private ISession _session => _httpContextAccessor.HttpContext.Session;
        public Sqls(IMemoryCache mcache, AppDbContext db, IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _mcache = mcache;
            _httpContextAccessor = httpContextAccessor;
        }

        public object SessionGet(string key, object data)
        {
            return _session.GetObject<W_USERS>("Kullanici");
        }
        public void SessionSet(string key, object data)
        {
            _session.SetObject(key, data);
        }

        public List<W_USERS> tumKullanicilargetir()
        {
            return _db.W_USERS.ToList();
        }

        public W_USERS KullaniciGetir(string kullanici)
        {
            W_USERS degisken = _session.GetObject<W_USERS>("Kullanici");
            return degisken;
        }
        public W_USERS KullaniciGetirAd(int id)
        {
            var bdbd = _db.W_USERS.Where(s => s.ID == id).FirstOrDefault();
            return _db.W_USERS.Where(s => s.ID == id).FirstOrDefault();
        }
    }
}