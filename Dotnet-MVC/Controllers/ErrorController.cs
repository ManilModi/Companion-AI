using Microsoft.AspNetCore.Mvc;

namespace DotnetMVCApp.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode?}")]
        public IActionResult HandleError(int? statusCode)
        {
            int code = statusCode ?? 404; // default to 404 if null
            ViewData["StatusCode"] = code;

            string message = code switch
            {
                404 => "Oops! The page you are looking for was not found.",
                500 => "Something went wrong on our side. Please try again later.",
                403 => "You don’t have permission to access this resource.",
                _ => "An unexpected error occurred."
            };

            ViewData["Message"] = message;
            return View("Error"); 
        }
    }
}
