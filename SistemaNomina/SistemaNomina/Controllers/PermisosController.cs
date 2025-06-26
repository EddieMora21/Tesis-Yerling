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
    public class PermisosController : Controller
    {
        // Conexión bd
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Muestra la lista de todos los permisos
        public ActionResult Index()
        {
            // Trae los permisos con sus datos relacionados 
            var permisos = db.Permisos.Include(p => p.Empleados).Include(p => p.Estados).Include(p => p.Usuarios).Include(p => p.TiposPermiso);
            return View(permisos.ToList());
        }

        // Muestra los detalles de un permiso en sí 
        public ActionResult Details(int? id)
        {
            // Si no se recibe un ID, devuelve un error
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            // Busca el permiso en la bd
            Permisos permisos = db.Permisos.Find(id);
            // Si no lo encuentra, devuelve error 
            if (permisos == null)
            {
                return HttpNotFound();
            }
            // Muestra los datos del permiso
            return View(permisos);
        }

        // Muestra el formulario para crear un nuevo permiso
        public ActionResult Create()
        {
            // Llena las listas desplegables con datos de otras tablas
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula");
            ViewBag.id_estado = new SelectList(db.Estados, "id_estado", "nombre");
            ViewBag.aprobado_por = new SelectList(db.Usuarios, "id_usuario", "usuario");
            ViewBag.id_tipo_permiso = new SelectList(db.TiposPermiso, "id_tipo_permiso", "nombre");
            return View();
        }

        // Procesa el formulario para crear un nuevo permiso
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "id_permiso,id_empleado,fecha,horas,id_tipo_permiso,motivo,id_estado,aprobado_por,fecha_creacion,fecha_actualizacion")] Permisos permisos)
        {
            // Verifica si los datos enviados son válidos
            if (ModelState.IsValid)
            {
                // Agrega el permiso a la base de datos y guarda los cambios
                db.Permisos.Add(permisos);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            // Si hay un error, vuelve a llenar las listas y muestra el formulario
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", permisos.id_empleado);
            ViewBag.id_estado = new SelectList(db.Estados, "id_estado", "nombre", permisos.id_estado);
            ViewBag.aprobado_por = new SelectList(db.Usuarios, "id_usuario", "usuario", permisos.aprobado_por);
            ViewBag.id_tipo_permiso = new SelectList(db.TiposPermiso, "id_tipo_permiso", "nombre", permisos.id_tipo_permiso);
            return View(permisos);
        }

        // Muestra el formulario para editar un permiso
        public ActionResult Edit(int? id)
        {
            // Verifica si se recibió un ID
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            // Busca el permiso
            Permisos permisos = db.Permisos.Find(id);
            // Si no lo encuentra, devuelve error
            if (permisos == null)
            {
                return HttpNotFound();
            }
            // Llena las listas desplegables con los datos actuales
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", permisos.id_empleado);
            ViewBag.id_estado = new SelectList(db.Estados, "id_estado", "nombre", permisos.id_estado);
            ViewBag.aprobado_por = new SelectList(db.Usuarios, "id_usuario", "usuario", permisos.aprobado_por);
            ViewBag.id_tipo_permiso = new SelectList(db.TiposPermiso, "id_tipo_permiso", "nombre", permisos.id_tipo_permiso);
            return View(permisos);
        }

        // Procesa el formulario para editar un permiso
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_permiso,id_empleado,fecha,horas,id_tipo_permiso,motivo,id_estado,aprobado_por,fecha_creacion,fecha_actualizacion")] Permisos permisos)
        {
            // Verifica si los datos son válidos
            if (ModelState.IsValid)
            {
                // Marca el permiso como modificado y guarda los cambios
                db.Entry(permisos).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            // Si hay error, vuelve a llenar las listas y muestra el formulario
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", permisos.id_empleado);
            ViewBag.id_estado = new SelectList(db.Estados, "id_estado", "nombre", permisos.id_estado);
            ViewBag.aprobado_por = new SelectList(db.Usuarios, "id_usuario", "usuario", permisos.aprobado_por);
            ViewBag.id_tipo_permiso = new SelectList(db.TiposPermiso, "id_tipo_permiso", "nombre", permisos.id_tipo_permiso);
            return View(permisos);
        }

        // Muestra la vista para confirmar la eliminación de un permiso
        public ActionResult Delete(int? id)
        {
            // Verifica si se recibió un ID
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            // Busca el permiso
            Permisos permisos = db.Permisos.Find(id);
            // Si no lo encuentra, devuelve error
            if (permisos == null)
            {
                return HttpNotFound();
            }
            return View(permisos);
        }

        // Confirma y realiza la eliminación del permiso
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            // Busca el permiso por ID
            Permisos permisos = db.Permisos.Find(id);
            // Lo elimina de la bd y guarda los cambios
            db.Permisos.Remove(permisos);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        // Libera los recursos 
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
