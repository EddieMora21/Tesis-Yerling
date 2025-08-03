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
    public class FeriadosController : Controller
    {
        // Conexión bd
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Muestra la lista de feriados
        public ActionResult Index()
        {
            return View(db.Feriados.ToList());
        }

        // Muestra detalles de un feriado específico
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Feriados feriados = db.Feriados.Find(id);
            if (feriados == null)
            {
                return HttpNotFound();
            }
            return View(feriados);
        }

        // Muestra el formulario para crear un feriado
        public ActionResult Create()
        {
            return View();
        }

        // Procesa los datos del formulario de creación
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "id_feriado,nombre,fecha,pago_obligatorio,descripcion")] Feriados feriados)
        {
            // ✅ VALIDACIÓN PARA EVITAR FECHAS DUPLICADAS - CORREGIDA
            if (feriados.fecha != null)
            {
                var fechaExistente = db.Feriados.Any(f => f.fecha == feriados.fecha);
                if (fechaExistente)
                {
                    ModelState.AddModelError("fecha", "Ya existe un feriado registrado para esta fecha. Por favor seleccione una fecha diferente.");
                }
            }

            if (ModelState.IsValid)
            {
                // Asignar fechas automáticamente
                feriados.fecha_creacion = DateTime.Now;
                feriados.fecha_actualizacion = DateTime.Now;

                db.Feriados.Add(feriados);
                db.SaveChanges();

                TempData["Success"] = "Feriado creado exitosamente.";
                return RedirectToAction("Index");
            }

            return View(feriados);
        }

        // Muestra el formulario para editar un feriado
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Feriados feriados = db.Feriados.Find(id);
            if (feriados == null)
            {
                return HttpNotFound();
            }
            return View(feriados);
        }

        // Procesa los datos del formulario de edición
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_feriado,nombre,fecha,pago_obligatorio,descripcion,fecha_creacion")] Feriados feriados)
        {
            // ✅ VALIDACIÓN PARA EVITAR FECHAS DUPLICADAS EN EDICIÓN - CORREGIDA
            if (feriados.fecha != null)
            {
                var fechaExistente = db.Feriados.Any(f => f.fecha == feriados.fecha && f.id_feriado != feriados.id_feriado);
                if (fechaExistente)
                {
                    ModelState.AddModelError("fecha", "Ya existe otro feriado registrado para esta fecha. Por favor seleccione una fecha diferente.");
                }
            }

            if (ModelState.IsValid)
            {
                // Actualizar solo la fecha de actualización
                feriados.fecha_actualizacion = DateTime.Now;

                db.Entry(feriados).State = EntityState.Modified;
                db.SaveChanges();

                TempData["Success"] = "Feriado actualizado exitosamente.";
                return RedirectToAction("Index");
            }
            return View(feriados);
        }

        // Muestra la confirmación para eliminar un feriado
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Feriados feriados = db.Feriados.Find(id);
            if (feriados == null)
            {
                return HttpNotFound();
            }
            return View(feriados);
        }

        // Confirma la eliminación de un feriado
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Feriados feriados = db.Feriados.Find(id);
            db.Feriados.Remove(feriados);
            db.SaveChanges();

            TempData["Success"] = "Feriado eliminado exitosamente.";
            return RedirectToAction("Index");
        }

        // Libera recursos del sistema
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