using System; 
using System.Collections.Generic;
using System.Data;
using System.Data.Entity; 
using System.Linq;
using System.Net; 
using System.Web;
using System.Web.Mvc;
using SistemaNomina.Models;
namespace SistemaNomina.Controllers
{
    public class TiposHoraExtraController : Controller // Maneja Tipos de Hora Extra
    {
        // Conexión bd
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Muestra la lista de todos los tipos de hora extra
        public ActionResult Index()
        {
            return View(db.TiposHoraExtra.ToList());
        }

        // Muestra los detalles de un tipo de hora extra por su ID
        public ActionResult Details(int? id)
        {
            // Si no se recibe el ID, se devuelve error
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Busca el registro en la base de datos
            TiposHoraExtra tiposHoraExtra = db.TiposHoraExtra.Find(id);

            // Si no se encuentra, se muestra error 
            if (tiposHoraExtra == null)
            {
                return HttpNotFound();
            }

            // Si se encuentra, se muestra la vista con los datos
            return View(tiposHoraExtra);
        }

        // Muestra el formulario para crear un nuevo tipo de hora extra
        public ActionResult Create()
        {
            return View();
        }

        // Guarda el nuevo tipo de hora extra en la base de datos
        [HttpPost]
        [ValidateAntiForgeryToken] // Seguridad
        public ActionResult Create([Bind(Include = "id_tipo,nombre,recargo,descripcion,fecha_creacion,fecha_actualizacion")] TiposHoraExtra tiposHoraExtra)
        {
            // Si los datos del formulario son válidos
            if (ModelState.IsValid)
            {
                // Agrega el nuevo tipo a la base de datos
                db.TiposHoraExtra.Add(tiposHoraExtra);
                db.SaveChanges(); // Guarda los cambios
                return RedirectToAction("Index"); // Redirige a la lista
            }

            // Si los datos no son válidos, vuelve a mostrar el formulario con errores
            return View(tiposHoraExtra);
        }

        // Muestra el formulario para editar un tipo de hora extra
        public ActionResult Edit(int? id)
        {
            // Si no se recibe ID, muestra error
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Busca el registro a editar
            TiposHoraExtra tiposHoraExtra = db.TiposHoraExtra.Find(id);

            // Si no se encuentra, muestra error
            if (tiposHoraExtra == null)
            {
                return HttpNotFound();
            }

            // Si se encuentra, muestra el formulario con los datos actuales
            return View(tiposHoraExtra);
        }

        // Guarda los cambios del tipo de hora extra editado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_tipo,nombre,recargo,descripcion,fecha_creacion,fecha_actualizacion")] TiposHoraExtra tiposHoraExtra)
        {
            // Verifica si los datos son válidos
            if (ModelState.IsValid)
            {
                // Marca el registro como modificado
                db.Entry(tiposHoraExtra).State = EntityState.Modified;
                db.SaveChanges(); // Guarda los cambios
                return RedirectToAction("Index"); // Redirige a la lista
            }

            // Si hay errores, vuelve a mostrar el formulario
            return View(tiposHoraExtra);
        }

        // Muestra la vista para confirmar eliminación de un tipo de hora extra
        public ActionResult Delete(int? id)
        {
            // Si no se recibe el ID, muestra error
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Busca el registro a eliminar
            TiposHoraExtra tiposHoraExtra = db.TiposHoraExtra.Find(id);

            // Si no existe, muestra error 
            if (tiposHoraExtra == null)
            {
                return HttpNotFound();
            }

            // Muestra la vista de confirmación
            return View(tiposHoraExtra);
        }

        // Elimina el tipo de hora extra después de confirmar
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            // Busca el registro por ID
            TiposHoraExtra tiposHoraExtra = db.TiposHoraExtra.Find(id);

            // Lo elimina de la base de datos
            db.TiposHoraExtra.Remove(tiposHoraExtra);
            db.SaveChanges(); // Guarda los cambios
            return RedirectToAction("Index"); // Redirige a la lista
        }

        // Libera los recursos 
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose(); // Libera la conexión a la base de datos
            }
            base.Dispose(disposing);
        }
    }
}
