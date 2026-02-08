using Microsoft.AspNetCore.Mvc;

namespace ERP.PL.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode}")]
        public IActionResult HttpStatusCodeHandler(int statusCode)
        {
            switch (statusCode)
            {
                case 404:
                    ViewBag.ErrorMessage = "The page you are looking for could not be found.";
                    ViewBag.ErrorTitle = "Page Not Found";
                    break;
                case 401:
                    ViewBag.ErrorMessage = "You are not authorized to access this page.";
                    ViewBag.ErrorTitle = "Unauthorized";
                    break;
                case 403:
                    ViewBag.ErrorMessage = "You do not have permission to access this resource.";
                    ViewBag.ErrorTitle = "Access Denied";
                    break;
                case 500:
                    ViewBag.ErrorMessage = "An internal server error occurred. Please try again later.";
                    ViewBag.ErrorTitle = "Server Error";
                    break;
                default:
                    ViewBag.ErrorMessage = "An unexpected error occurred.";
                    ViewBag.ErrorTitle = "Error";
                    break;
            }

            return View("StatusCode");
        }
    }
}
