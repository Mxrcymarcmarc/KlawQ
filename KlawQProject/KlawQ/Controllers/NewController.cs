using Microsoft.AspNetCore.Mvc;
using System.Text.Encodings.Web;

namespace KlawQ.Controllers
{
    public class NewController : Controller
    {
        // GET: /HelloWorld/
        public IActionResult Index()
        {
            return View();
        }
        // GET: /HelloWorld/Welcome/ 
        public string Welcome(string name, int ID = 1)
        {
            return HtmlEncoder.Default.Encode($"Hello! {name}, ID is: {ID}");
        }
    }
}
