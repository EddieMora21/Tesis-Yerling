using System.Web;
using System.Web.Mvc;
using SistemaNomina.Filters;

namespace SistemaNomina
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            // Filtro para manejo de errores
            filters.Add(new HandleErrorAttribute());
        }
    }
}