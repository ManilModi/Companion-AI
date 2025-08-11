//using Microsoft.AspNetCore.Mvc;
////using DotnetMVCApp.Services;
//using DotnetMVCApp.Models;
//using System.Linq;
//using MongoDB.Driver;

//namespace DotnetMVCApp.Controllers
//{
//    public class UserController : Controller
//    {
//        private readonly MongoDbContext _context;

//        public UserController(MongoDbContext context)
//        {
//            _context = context;
//        }

//        public IActionResult Index()
//        {
//            var users = _context.Tests.Find(_ => true).ToList();
//            return View(users);
//        }
//    }
//}
