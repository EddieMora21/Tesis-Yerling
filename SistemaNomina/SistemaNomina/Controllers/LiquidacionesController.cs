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
    public class LiquidacionesController : Controller
    {
        // Conexión bd
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Muestra la lista de todas las liquidaciones
        public ActionResult Index()
        {
            var liquidaciones = db.Liquidaciones.Include(l => l.Empleados).Include(l => l.TipoLiquidacion);
            return View(liquidaciones.ToList());
        }

        // Muestra los detalles de una liquidación en sí
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                // Si no se recibe un ID válido, se devuelve error
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Liquidaciones liquidaciones = db.Liquidaciones.Find(id);
            if (liquidaciones == null)
            {
                // Si no se encuentra la liquidación, se muestra error 
                return HttpNotFound();
            }
            return View(liquidaciones);
        }

        // Muestra el formulario para crear una nueva liquidación
        public ActionResult Create()
        {
            // Llena los combos con empleados y tipos de liquidación
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula");
            ViewBag.id_tipo = new SelectList(db.TipoLiquidacion, "id_tipo", "nombre");
            return View();
        }

        // Guarda la nueva liquidación en bd
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "id_liquidacion,id_empleado,id_tipo,fecha_salida,preaviso,cesantia,vacaciones_pendientes,dias_vacaciones_pendientes,aguinaldo_proporcional,total_liquidacion,isr_liquidacion,css_liquidacion,ivm_liquidacion,fecha_creacion,fecha_actualizacion")] Liquidaciones liquidaciones)
        {
            if (ModelState.IsValid)
            {
                db.Liquidaciones.Add(liquidaciones); // Agrega la nueva liquidación
                db.SaveChanges(); // Guarda cambios
                return RedirectToAction("Index"); // Redirige a la lista
            }

            // Si hay errores, se vuelve a cargar el formulario con los combos
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", liquidaciones.id_empleado);
            ViewBag.id_tipo = new SelectList(db.TipoLiquidacion, "id_tipo", "nombre", liquidaciones.id_tipo);
            return View(liquidaciones);
        }

        // Muestra el formulario para editar una liquidación existente
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                // Error si el ID es nulo
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Liquidaciones liquidaciones = db.Liquidaciones.Find(id);
            if (liquidaciones == null)
            {
                // Error si no se encuentra la liquidación
                return HttpNotFound();
            }

            // Llena los combos con los datos actuales
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", liquidaciones.id_empleado);
            ViewBag.id_tipo = new SelectList(db.TipoLiquidacion, "id_tipo", "nombre", liquidaciones.id_tipo);
            return View(liquidaciones);
        }

        // Guarda los cambios al editar una liquidación
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_liquidacion,id_empleado,id_tipo,fecha_salida,preaviso,cesantia,vacaciones_pendientes,dias_vacaciones_pendientes,aguinaldo_proporcional,total_liquidacion,isr_liquidacion,css_liquidacion,ivm_liquidacion,fecha_creacion,fecha_actualizacion")] Liquidaciones liquidaciones)
        {
            if (ModelState.IsValid)
            {
                db.Entry(liquidaciones).State = EntityState.Modified; // Marca como modificada
                db.SaveChanges(); // Guarda cambios
                return RedirectToAction("Index"); // Redirige a la lista
            }

            // Si hay errores, vuelve a mostrar el formulario
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", liquidaciones.id_empleado);
            ViewBag.id_tipo = new SelectList(db.TipoLiquidacion, "id_tipo", "nombre", liquidaciones.id_tipo);
            return View(liquidaciones);
        }

        // Muestra la confirmación para eliminar una liquidación
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                // Error si el ID es nulo
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Liquidaciones liquidaciones = db.Liquidaciones.Find(id);
            if (liquidaciones == null)
            {
                // Error si no se encuentra la liquidación
                return HttpNotFound();
            }
            return View(liquidaciones);
        }

        // Elimina la liquidación confirmada
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Liquidaciones liquidaciones = db.Liquidaciones.Find(id);
            db.Liquidaciones.Remove(liquidaciones); // Elimina la liquidación
            db.SaveChanges(); // Guarda cambios
            return RedirectToAction("Index"); // Redirige a la lista
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
