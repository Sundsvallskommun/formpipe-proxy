using System.Web.Mvc;

namespace FormpipeProxy.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return Redirect("~/swagger");
        }
    }
}
