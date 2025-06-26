using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using SistemaNomina.Models;
using SistemaNomina.Filters;
using SistemaNomina.Helpers;

namespace SistemaNomina.Controllers
{
    [RoleAuthorize("Admin", "RRHH")] // ✅ Solo Admin y RRHH pueden gestionar incapacidades
    public class IncapacidadesController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // GET: Incapacidades
        public ActionResult Index()
        {
            try
            {
                var incapacidades = db.Incapacidades
                    .Include(i => i.Empleados)
                    .Include(i => i.Estados)
                    .Include(i => i.TipoIncapacidades)
                    .OrderByDescending(i => i.fecha_registro)
                    .ToList();

                BitacoraHelper.RegistrarAccion("CONSULTA_INCAPACIDADES", "Consultó listado de incapacidades");
                return View(incapacidades);
            }
            catch (Exception ex)
            {
                BitacoraHelper.RegistrarAccion("ERROR_CONSULTA_INCAPACIDADES", $"Error: {ex.Message}");
                TempData["Error"] = "Error al cargar las incapacidades.";
                return View(new List<Incapacidades>());
            }
        }

        // GET: Incapacidades/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Incapacidades incapacidad = db.Incapacidades
                .Include(i => i.Empleados)
                .Include(i => i.Estados)
                .Include(i => i.TipoIncapacidades)
                .FirstOrDefault(i => i.id_incapacidad == id);

            if (incapacidad == null)
            {
                return HttpNotFound();
            }

            BitacoraHelper.RegistrarAccion("VER_INCAPACIDAD", $"Consultó detalles de incapacidad ID: {id}");
            return View(incapacidad);
        }

        // GET: Incapacidades/Create
        public ActionResult Create()
        {
            CargarViewBags();
            return View();
        }

        // POST: Incapacidades/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "id_empleado,fecha_inicio,fecha_fin,numero_boleta,id_tipo_incapacidad,descripcion,id_estado")] Incapacidades incapacidad)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // ✅ Validaciones de negocio
                    if (incapacidad.fecha_fin <= incapacidad.fecha_inicio)
                    {
                        ModelState.AddModelError("fecha_fin", "La fecha fin debe ser posterior a la fecha inicio.");
                        CargarViewBags(incapacidad);
                        return View(incapacidad);
                    }

                    // ✅ Cálculo automático de días
                    incapacidad.dias_incapacidad = (int)(incapacidad.fecha_fin - incapacidad.fecha_inicio).TotalDays + 1;
                    incapacidad.fecha_registro = DateTime.Now;

                    // ✅ Estado por defecto si no se selecciona
                    if (incapacidad.id_estado == 0)
                    {
                        var estadoPendiente = db.Estados.FirstOrDefault(e => e.nombre == "Pendiente" && e.modulo == "Incapacidades");
                        incapacidad.id_estado = estadoPendiente?.id_estado ?? 1;
                    }

                    db.Incapacidades.Add(incapacidad);
                    db.SaveChanges();

                    // ✅ Registrar en bitácora
                    var empleado = db.Empleados.Find(incapacidad.id_empleado);
                    BitacoraHelper.RegistrarAccion("CREAR_INCAPACIDAD",
                        $"Registró incapacidad para {empleado?.nombre1} {empleado?.apellido1} del {incapacidad.fecha_inicio:dd/MM/yyyy} al {incapacidad.fecha_fin:dd/MM/yyyy}");

                    TempData["Success"] = "Incapacidad registrada exitosamente.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                BitacoraHelper.RegistrarAccion("ERROR_CREAR_INCAPACIDAD", $"Error: {ex.Message}");
                TempData["Error"] = "Error al registrar la incapacidad.";
            }

            CargarViewBags(incapacidad);
            return View(incapacidad);
        }

        // GET: Incapacidades/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Incapacidades incapacidad = db.Incapacidades.Find(id);
            if (incapacidad == null)
            {
                return HttpNotFound();
            }

            CargarViewBags(incapacidad);
            return View(incapacidad);
        }

        // POST: Incapacidades/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_incapacidad,id_empleado,fecha_inicio,fecha_fin,numero_boleta,id_tipo_incapacidad,descripcion,id_estado,dias_incapacidad,fecha_registro")] Incapacidades incapacidad)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // ✅ Recalcular días automáticamente
                    if (incapacidad.fecha_fin > incapacidad.fecha_inicio)
                    {
                        incapacidad.dias_incapacidad = (int)(incapacidad.fecha_fin - incapacidad.fecha_inicio).TotalDays + 1;
                    }

                    db.Entry(incapacidad).State = EntityState.Modified;
                    db.SaveChanges();

                    // ✅ Registrar en bitácora
                    var empleado = db.Empleados.Find(incapacidad.id_empleado);
                    BitacoraHelper.RegistrarAccion("EDITAR_INCAPACIDAD",
                        $"Editó incapacidad ID: {incapacidad.id_incapacidad} de {empleado?.nombre1} {empleado?.apellido1}");

                    TempData["Success"] = "Incapacidad actualizada exitosamente.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                BitacoraHelper.RegistrarAccion("ERROR_EDITAR_INCAPACIDAD", $"Error: {ex.Message}");
                TempData["Error"] = "Error al actualizar la incapacidad.";
            }

            CargarViewBags(incapacidad);
            return View(incapacidad);
        }

        // GET: Incapacidades/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Incapacidades incapacidad = db.Incapacidades
                .Include(i => i.Empleados)
                .Include(i => i.Estados)
                .Include(i => i.TipoIncapacidades)
                .FirstOrDefault(i => i.id_incapacidad == id);

            if (incapacidad == null)
            {
                return HttpNotFound();
            }

            return View(incapacidad);
        }

        // POST: Incapacidades/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                Incapacidades incapacidad = db.Incapacidades.Find(id);
                var empleado = db.Empleados.Find(incapacidad.id_empleado);

                db.Incapacidades.Remove(incapacidad);
                db.SaveChanges();

                // ✅ Registrar en bitácora
                BitacoraHelper.RegistrarAccion("ELIMINAR_INCAPACIDAD",
                    $"Eliminó incapacidad ID: {id} de {empleado?.nombre1} {empleado?.apellido1}");

                TempData["Success"] = "Incapacidad eliminada exitosamente.";
            }
            catch (Exception ex)
            {
                BitacoraHelper.RegistrarAccion("ERROR_ELIMINAR_INCAPACIDAD", $"Error: {ex.Message}");
                TempData["Error"] = "Error al eliminar la incapacidad.";
            }

            return RedirectToAction("Index");
        }

        // ✅ Método auxiliar para cargar ViewBags
        private void CargarViewBags(Incapacidades incapacidad = null)
        {
            ViewBag.id_empleado = new SelectList(
                db.Empleados.Where(e => e.estado == "Activo")
                           .Select(e => new {
                               e.id_empleado,
                               NombreCompleto = e.nombre1 + " " + e.apellido1 + " - " + e.cedula
                           }),
                "id_empleado", "NombreCompleto", incapacidad?.id_empleado);

            ViewBag.id_tipo_incapacidad = new SelectList(db.TipoIncapacidades, "id_tipo", "nombre", incapacidad?.id_tipo_incapacidad);

            ViewBag.id_estado = new SelectList(
                db.Estados.Where(e => e.modulo == "Incapacidades"),
                "id_estado", "nombre", incapacidad?.id_estado);
        }

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