using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using SistemaNomina;
using SistemaNomina.Models;

namespace SistemaNomina.Controllers
{
    public class BitacoraController : Controller
    {
        // Conecta a la bd
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Lista completa de registros en la bitácora
        public ActionResult Index()
        {
            var bitacora = db.Bitacora.Include(b => b.Usuarios); // Trae información del usuario relacionado
            return View(bitacora.ToList());
        }

        // Detalles de un registro específico de la bitácora
        public ActionResult Details(int? id)
        {
            if (id == null) // Si no se pasa un ID, muestra error
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Bitacora bitacora = db.Bitacora.Find(id); // Busca registro por ID
            if (bitacora == null) // No se encuentra, muestra error
            {
                return HttpNotFound();
            }
            return View(bitacora);
        }

        // Muestra formulario para crear un nuevo registro en la bitácora
        public ActionResult Create()
        {
            // Llena el campo de usuarios con los datos de la base
            ViewBag.id_usuario = new SelectList(db.Usuarios, "id_usuario", "usuario");
            return View();
        }

        // Guarda registro bd
        [HttpPost]
        [ValidateAntiForgeryToken] // Seguridad para evitar envíos maliciosos, o sea, contra ataques 
        public ActionResult Create([Bind(Include = "id_log,id_usuario,accion,detalle,fecha_hora")] Bitacora bitacora)
        {
            if (ModelState.IsValid) // Verifica que los datos sean correctos
            {
                db.Bitacora.Add(bitacora); // Agrega el nuevo registro
                db.SaveChanges(); // Guarda los cambios en la base
                return RedirectToAction("Index"); // Vuelve a la lista
            }

            // Hay error, vuelve a mostrar el formulario con los datos ya escritos
            ViewBag.id_usuario = new SelectList(db.Usuarios, "id_usuario", "usuario", bitacora.id_usuario);
            return View(bitacora);
        }

        // Muestra el formulario para editar un registro existente
        public ActionResult Edit(int? id)
        {
            if (id == null) // Si no se pasa un ID, muestra error
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Bitacora bitacora = db.Bitacora.Find(id); // Busca el registro
            if (bitacora == null) // Si no lo encuentra, muestra error
            {
                return HttpNotFound();
            }
            // Llena el campo de usuario con los datos actuales
            ViewBag.id_usuario = new SelectList(db.Usuarios, "id_usuario", "usuario", bitacora.id_usuario);
            return View(bitacora);
        }

        // Guarda los cambios hechos en la edición del registro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_log,id_usuario,accion,detalle,fecha_hora")] Bitacora bitacora)
        {
            if (ModelState.IsValid) // Verifica que los datos sean correctos
            {
                db.Entry(bitacora).State = EntityState.Modified; // Marca como modificado
                db.SaveChanges(); // Guarda los cambios
                return RedirectToAction("Index");
            }
            // Hay error, vuelve al formulario con los datos
            ViewBag.id_usuario = new SelectList(db.Usuarios, "id_usuario", "usuario", bitacora.id_usuario);
            return View(bitacora);
        }

        // Muestra la pantalla para confirmar que se quiere borrar el registro
        public ActionResult Delete(int? id)
        {
            if (id == null) // Si no se pasa un ID, muestra error
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Bitacora bitacora = db.Bitacora.Find(id); // Busca el registro
            if (bitacora == null) // Si no lo encuentra, muestra error
            {
                return HttpNotFound();
            }
            return View(bitacora);
        }

        // Elimina el registro de la bd
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Bitacora bitacora = db.Bitacora.Find(id); // Busca el registro
            db.Bitacora.Remove(bitacora); // Lo elimina
            db.SaveChanges(); // Guarda los cambios
            return RedirectToAction("Index");
        }

        // Libera los recursos del sistema cuando ya no se usan
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose(); // Cierra la conexión con la base
            }
            base.Dispose(disposing);
        }
    }
}
