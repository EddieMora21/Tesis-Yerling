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
    public class ISRController : Controller
    {
        // Conexión bd 
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Muestra la lista de registros
        public ActionResult Index()
        {
            return View(db.ISR.ToList());
        }

        // Muestra los detalles de un registro
        public ActionResult Details(int? id)
        {
            // Si no se envía un ID, devuelve error
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Busca el registro con ese ID
            ISR iSR = db.ISR.Find(id);

            // Si no existe, devuelve error
            if (iSR == null)
            {
                return HttpNotFound();
            }

            // Si lo encuentra, lo muestra
            return View(iSR);
        }

        // Muestra el formulario para crear un nuevo registro 
        public ActionResult Create()
        {
            return View();
        }

        // Recibe los datos del formulario para crear un nuevo registro 
        [HttpPost]
        [ValidateAntiForgeryToken] // Protección contra ataques 
        public ActionResult Create([Bind(Include = "id_isr,anio,limite_inferior,limite_superior,porcentaje,exceso,credito_hijo,credito_conyuge,descripcion,fecha_creacion,fecha_actualizacion")] ISR iSR)
        {
            // Si los datos del formulario son válidos
            if (ModelState.IsValid)
            {
                // Guarda el nuevo registro en la base de datos
                db.ISR.Add(iSR);
                db.SaveChanges();

                // Redirige a la lista
                return RedirectToAction("Index");
            }

            // Si hay errores, vuelve a mostrar el formulario con los datos ingresados
            return View(iSR);
        }

        // Muestra el formulario para editar un registro existente
        public ActionResult Edit(int? id)
        {
            // Si no se envía un ID, devuelve error
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Busca el registro en la bd
            ISR iSR = db.ISR.Find(id);

            // Si no existe, devuelve error
            if (iSR == null)
            {
                return HttpNotFound();
            }

            // Si lo encuentra, muestra el formulario con los datos actuales
            return View(iSR);
        }

        // Recibe los datos editados y los guarda en bd
        [HttpPost]
        [ValidateAntiForgeryToken] // Protección contra ataques 
        public ActionResult Edit([Bind(Include = "id_isr,anio,limite_inferior,limite_superior,porcentaje,exceso,credito_hijo,credito_conyuge,descripcion,fecha_creacion,fecha_actualizacion")] ISR iSR)
        {
            // Si los datos son válidos
            if (ModelState.IsValid)
            {
                // Marca el registro como modificado y guarda los cambios
                db.Entry(iSR).State = EntityState.Modified;
                db.SaveChanges();

                // Redirige a la lista
                return RedirectToAction("Index");
            }

            // Si hay errores, vuelve a mostrar el formulario
            return View(iSR);
        }

        // Muestra la confirmación para eliminar un registro
        public ActionResult Delete(int? id)
        {
            // Si no se envía un ID, devuelve error
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            // Busca el registro
            ISR iSR = db.ISR.Find(id);

            // Si no existe, devuelve error
            if (iSR == null)
            {
                return HttpNotFound();
            }

            // Si lo encuentra, muestra los detalles para confirmar eliminación
            return View(iSR);
        }

        // Confirma y elimina el registro de la bd
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            // Busca el registro
            ISR iSR = db.ISR.Find(id);

            // Lo elimina
            db.ISR.Remove(iSR);
            db.SaveChanges();

            // Redirige a la lista
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
