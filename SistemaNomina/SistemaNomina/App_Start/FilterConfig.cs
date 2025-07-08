using System.Web.Mvc;
using SistemaNomina.Filters;

public class FilterConfig
{
    public static void RegisterGlobalFilters(GlobalFilterCollection filters)
    {
        filters.Add(new HandleErrorAttribute());

        // 🔧 AGREGAR VALIDACIÓN DE SESIÓN GLOBAL
        filters.Add(new SessionValidationAttribute());
    }
}