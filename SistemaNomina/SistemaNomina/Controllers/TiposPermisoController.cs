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
    public class TiposPermisoController : Controller // Gestiona tipos de permisos
    {
        // Conexión bd
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Muestra la lista de todos los tipos de permiso
        public ActionResult Index()
        {
            return View(db.TiposPermiso.ToList());
        }

        // Muestra los detalles de un permiso según su ID
        public ActionResult Details(int? id)
        {
            // Si no se recibe un ID, devolver un error 
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Buscar en la base de datos el tipo de permiso con ese ID
            TiposPermiso tiposPermiso = db.TiposPermiso.Find(id);

            // Si no existe el permiso, mostrar error 
            if (tiposPermiso == null)
            {
                return HttpNotFound();
            }

            // Mostrar la vista con la información del permiso
            return View(tiposPermiso);
        }

        // Muestra el formulario para crear un nuevo tipo de permiso
        public ActionResult Create()
        {
            return View();
        }

        // Guarda el nuevo permiso en la base de datos cuando se envía el formulario
        [HttpPost]
        [ValidateAntiForgeryToken] 
        public ActionResult Create([Bind(Include = "id_tipo_permiso,nombre,con_goce,descripcion,fecha_creacion,fecha_actualizacion")] TiposPermiso tiposPermiso)
        {
            // Verifica que los datos enviados sean correctos
            if (ModelState.IsValid)
            {
                // Agrega el nuevo permiso a la base de datos
                db.TiposPermiso.Add(tiposPermiso);
                db.SaveChanges(); // Guarda los cambios
                return RedirectToAction("Index"); // Redirige a la lista de permisos
            }

            // Si los datos no son válidos, vuelve a mostrar el formulario con errores
            return View(tiposPermiso);
        }

        // Muestra el formulario para editar un permiso existente
        public ActionResult Edit(int? id)
        {
            // Si no llega un ID, devolver error 
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Buscar el permiso a editar
            TiposPermiso tiposPermiso = db.TiposPermiso.Find(id);

            // Si no existe, devolver error 
            if (tiposPermiso == null)
            {
                return HttpNotFound();
            }

            // Mostrar el formulario con los datos actuales del permiso
            return View(tiposPermiso);
        }

        // Guarda los cambios hechos al permiso después de editarlo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_tipo_permiso,nombre,con_goce,descripcion,fecha_creacion,fecha_actualizacion")] TiposPermiso tiposPermiso)
        {
            // Verifica que los datos estén correctos
            if (ModelState.IsValid)
            {
                // Marca el permiso como modificado
                db.Entry(tiposPermiso).State = EntityState.Modified;
                db.SaveChanges(); // Guarda los cambios en la base de datos
                return RedirectToAction("Index"); // Regresa a la lista de permisos
            }

            // Si hay errores, vuelve a mostrar el formulario para corregir
            return View(tiposPermiso);
        }

        // Muestra la vista para confirmar que se quiere eliminar un permiso
        public ActionResult Delete(int? id)
        {
            // Si no llega ID, error
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Buscar el permiso que se quiere eliminar
            TiposPermiso tiposPermiso = db.TiposPermiso.Find(id);

            // Si no existe, error 
            if (tiposPermiso == null)
            {
                return HttpNotFound();
            }

            // Mostrar vista para confirmar eliminación
            return View(tiposPermiso);
        }

        // Elimina el permiso después de confirmar
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            // Buscar el permiso por ID
            TiposPermiso tiposPermiso = db.TiposPermiso.Find(id);

            // Eliminar de la base de datos
            db.TiposPermiso.Remove(tiposPermiso);
            db.SaveChanges(); // Guardar cambios
            return RedirectToAction("Index"); // Volver a la lista
        }

        // Limpia la conexión 
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose(); // Libera recursos de la base de datos
            }
            base.Dispose(disposing);
        }
    }
}
