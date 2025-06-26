using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using SistemaNomina.Models;

namespace SistemaNomina.Controllers
{
    public class HomeController : Controller
    {
        
        public ActionResult Index()
        {
            // Verificar si el usuario está autenticado
            if (!User.Identity.IsAuthenticated)
            {
                // Si no está autenticado, redirigir al login
                return RedirectToAction("Login", "Usuarios");
            }

            // Si está autenticado, continuar con la carga de la página
            return View();
        }

        // Mostrar la página 
        public ActionResult About()
        {
            ViewBag.Message = "Página de descripción de la aplicación.";
            return View();
        }

        // Acción para mostrar la página de contacto
        public ActionResult Contact()
        {
            ViewBag.Message = "Página de contacto.";
            return View();
        }

        // Acción para procesar el logout y redirigir al login
        public ActionResult Logout()
        {
            // Cerrar sesión
            FormsAuthentication.SignOut();

            // Redirigir al login
            return RedirectToAction("Login", "Usuarios");
        }
    }
}
