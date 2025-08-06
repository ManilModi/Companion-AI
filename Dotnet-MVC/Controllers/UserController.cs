using Microsoft.AspNetCore.Mvc;
using DotnetMVCApp.Services;
using MongoDB.Driver;
using System.Collections.Generic;

namespace DotnetMVCApp.Controllers
{
    public class User
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class UserController : Controller
    {
        private readonly MongoDBService _mongoDBService;

        public UserController(MongoDBService mongoDBService)
        {
            _mongoDBService = mongoDBService;
        }

        public IActionResult Index()
        {
            var usersCollection = _mongoDBService.GetCollection<User>("Users");
            var users = usersCollection.Find(_ => true).ToList();
            return View(users);
        }
    }
}
