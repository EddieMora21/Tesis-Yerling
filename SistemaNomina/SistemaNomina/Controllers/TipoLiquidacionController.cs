using System; // Importa funcionalidades básicas del sistema.
using System.Collections.Generic; // Permite usar listas y colecciones genéricas.
using System.Data; // Proporciona acceso a clases de datos.
using System.Data.Entity; // Permite trabajar con bases de datos usando Entity Framework.
using System.Linq; // Proporciona métodos para consultar datos (LINQ).
using System.Net; // Permite trabajar con protocolos de red 
using System.Web; // Permite trabajar con funcionalidades de aplicaciones web.
using System.Web.Mvc; // Permite usar el patrón MVC para aplicaciones web.
using SistemaNomina.Models;
namespace SistemaNomina.Controllers
{
    public class TipoLiquidacionController : Controller
    {
        // Conexión bd
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Muestra una lista con todos los tipos de liquidación.
        public ActionResult Index()
        {
            return View(db.TipoLiquidacion.ToList()); // Trae todos los registros y los pasa a la vista.
        }

        // Muestra los detalles de un tipo de liquidación específico.
        public ActionResult Details(int? id)
        {
            if (id == null) // Si no se envía el ID, devuelve error.
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Busca el tipo de liquidación con el ID recibido.
            TipoLiquidacion tipoLiquidacion = db.TipoLiquidacion.Find(id);

            if (tipoLiquidacion == null) // Si no lo encuentra, muestra error.
            {
                return HttpNotFound();
            }

            return View(tipoLiquidacion); // Muestra la vista con los datos encontrados.
        }

        // Muestra el formulario para crear un nuevo tipo de liquidación.
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost] 
        [ValidateAntiForgeryToken] 
        public ActionResult Create([Bind(Include = "id_tipo,nombre,descripcion,fecha_creacion,fecha_actualizacion")] TipoLiquidacion tipoLiquidacion)
        {
            if (ModelState.IsValid) // Verifica que los datos recibidos sean válidos.
            {
                db.TipoLiquidacion.Add(tipoLiquidacion); // Agrega el nuevo tipo de liquidación a la base de datos.
                db.SaveChanges(); // Guarda los cambios.
                return RedirectToAction("Index"); // Redirige a la lista de tipos de liquidación.
            }

            return View(tipoLiquidacion); // Si hay errores, vuelve a mostrar el formulario con los datos.
        }

        // Muestra el formulario para editar un tipo de liquidación existente.
        public ActionResult Edit(int? id)
        {
            if (id == null) // Si no se envía el ID, muestra error.
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Busca el tipo de liquidación con el ID recibido.
            TipoLiquidacion tipoLiquidacion = db.TipoLiquidacion.Find(id);

            if (tipoLiquidacion == null) // Si no lo encuentra, muestra error.
            {
                return HttpNotFound();
            }

            return View(tipoLiquidacion); // Muestra la vista para editar los datos encontrados.
        }

        // Recibe los datos del formulario para actualizar un tipo de liquidación.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_tipo,nombre,descripcion,fecha_creacion,fecha_actualizacion")] TipoLiquidacion tipoLiquidacion)
        {
            if (ModelState.IsValid) // Verifica que los datos sean válidos.
            {
                db.Entry(tipoLiquidacion).State = EntityState.Modified; // Marca el objeto como modificado.
                db.SaveChanges(); // Guarda los cambios en la base de datos.
                return RedirectToAction("Index"); // Redirige a la lista.
            }
            return View(tipoLiquidacion); // Si hay errores, vuelve a mostrar el formulario.
        }

        // Muestra la vista de confirmación para eliminar un tipo de liquidación.
        public ActionResult Delete(int? id)
        {
            if (id == null) // Si no se envía el ID, muestra error.
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            TipoLiquidacion tipoLiquidacion = db.TipoLiquidacion.Find(id); // Busca el tipo de liquidación.

            if (tipoLiquidacion == null) // Si no lo encuentra, muestra error.
            {
                return HttpNotFound();
            }

            return View(tipoLiquidacion); // Muestra la vista de confirmación.
        }

        // Método que se ejecuta al confirmar la eliminación del tipo de liquidación.
        [HttpPost, ActionName("Delete")] 
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            TipoLiquidacion tipoLiquidacion = db.TipoLiquidacion.Find(id); // Busca el tipo de liquidación.
            db.TipoLiquidacion.Remove(tipoLiquidacion); // Lo elimina de la base de datos.
            db.SaveChanges(); // Guarda los cambios.
            return RedirectToAction("Index"); // Redirige a la lista.
        }

        // Libera los recursos .
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose(); // Libera la conexión a la base de datos.
            }
            base.Dispose(disposing); 
        }
    }
}
