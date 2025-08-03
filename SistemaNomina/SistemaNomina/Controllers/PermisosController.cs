using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using SistemaNomina.Models;
using SistemaNomina.Filters;
using SistemaNomina.Helpers;

namespace SistemaNomina.Controllers
{
    [RoleAuthorize("Admin", "RRHH", "IT")]
    public class PermisosController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // GET: Permisos
        // GET: Permisos
        public ActionResult Index()
        {
            try
            {
                var permisos = db.Permisos
                    .Include(p => p.Empleados)
                    .Include(p => p.Estados)
                    .Include(p => p.Usuarios)
                    .Include(p => p.TiposPermiso)
                    .OrderByDescending(p => p.fecha_creacion)
                    .ToList();

                // ✅ AGREGAR ESTA LÍNEA - Cargar tipos de permiso para el filtro
                ViewBag.TiposPermiso = db.TiposPermiso.OrderBy(t => t.nombre).ToList();

                return View(permisos);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar permisos: " + ex.Message;

                // ✅ AGREGAR TAMBIÉN AQUÍ para el caso de error
                ViewBag.TiposPermiso = db.TiposPermiso.OrderBy(t => t.nombre).ToList();

                return View(new List<Permisos>());
            }
        }

        // GET: Permisos/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Permisos permiso = db.Permisos
                .Include(p => p.Empleados)
                .Include(p => p.Estados)
                .Include(p => p.Usuarios)
                .Include(p => p.TiposPermiso)
                .FirstOrDefault(p => p.id_permiso == id);

            if (permiso == null)
                return HttpNotFound();

            return View(permiso);
        }

        // GET: Permisos/Create
        public ActionResult Create()
        {
            CargarListas();
            return View();
        }

        // POST: Permisos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "id_empleado,fecha,horas,id_tipo_permiso,motivo")] Permisos permiso)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Validaciones de negocio
                    if (permiso.fecha < DateTime.Today)
                    {
                        ModelState.AddModelError("fecha", "No se puede solicitar un permiso para fechas pasadas.");
                        CargarListas(permiso);
                        return View(permiso);
                    }

                    if (permiso.horas <= 0)
                    {
                        ModelState.AddModelError("horas", "Las horas deben ser mayor a 0.");
                        CargarListas(permiso);
                        return View(permiso);
                    }

                    // Validar que no exista un permiso duplicado para la misma fecha y empleado
                    var permisoExistente = db.Permisos.Any(p => p.id_empleado == permiso.id_empleado
                                                             && DbFunctions.TruncateTime(p.fecha) == DbFunctions.TruncateTime(permiso.fecha));
                    if (permisoExistente)
                    {
                        ModelState.AddModelError("fecha", "Ya existe un permiso para esta fecha.");
                        CargarListas(permiso);
                        return View(permiso);
                    }

                    // Establecer estado inicial como "Pendiente"
                    var estadoPendiente = db.Estados.FirstOrDefault(e => e.nombre == "Pendiente" && e.modulo == "Permisos");
                    permiso.id_estado = estadoPendiente?.id_estado ?? 1;

                    // Establecer fechas
                    permiso.fecha_creacion = DateTime.Now;
                    permiso.fecha_actualizacion = DateTime.Now;

                    // aprobado_por debe ser null inicialmente
                    permiso.aprobado_por = null;

                    db.Permisos.Add(permiso);
                    db.SaveChanges();

                    // Log automático
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId!= null)
                    {
                        BitacoraHelper.RegistrarAccion("CREAR_PERMISO",
                            $"Creado permiso para empleado ID: {permiso.id_empleado}",
                            currentUserId.Value);
                    }

                    TempData["Success"] = "Permiso creado exitosamente.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al crear el permiso: " + ex.Message;
            }

            CargarListas(permiso);
            return View(permiso);
        }

        // GET: Permisos/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Permisos permiso = db.Permisos.Find(id);
            if (permiso == null)
                return HttpNotFound();

            // Solo permitir editar permisos pendientes
            var estadoPendiente = db.Estados.FirstOrDefault(e => e.nombre == "Pendiente" && e.modulo == "Permisos");
            if (permiso.id_estado != estadoPendiente?.id_estado)
            {
                TempData["Error"] = "Solo se pueden editar permisos en estado Pendiente.";
                return RedirectToAction("Index");
            }

            CargarListas(permiso);
            return View(permiso);
        }

        // POST: Permisos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_permiso,id_empleado,fecha,horas,id_tipo_permiso,motivo,id_estado")] Permisos permiso)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Validaciones de negocio
                    if (permiso.fecha < DateTime.Today)
                    {
                        ModelState.AddModelError("fecha", "No se puede solicitar un permiso para fechas pasadas.");
                        CargarListas(permiso);
                        return View(permiso);
                    }

                    if (permiso.horas <= 0)
                    {
                        ModelState.AddModelError("horas", "Las horas deben ser mayor a 0.");
                        CargarListas(permiso);
                        return View(permiso);
                    }

                    // Obtener el permiso original para mantener campos que no deben cambiar
                    var permisoOriginal = db.Permisos.AsNoTracking().FirstOrDefault(p => p.id_permiso == permiso.id_permiso);
                    if (permisoOriginal != null)
                    {
                        permiso.fecha_creacion = permisoOriginal.fecha_creacion;
                        permiso.aprobado_por = permisoOriginal.aprobado_por;
                    }

                    permiso.fecha_actualizacion = DateTime.Now;
                    db.Entry(permiso).State = EntityState.Modified;
                    db.SaveChanges();

                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId!= null)
                    {
                        BitacoraHelper.RegistrarAccion("EDITAR_PERMISO",
                            $"Editado permiso ID: {permiso.id_permiso}",
                            currentUserId.Value);
                    }

                    TempData["Success"] = "Permiso actualizado exitosamente.";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar el permiso: " + ex.Message;
            }

            CargarListas(permiso);
            return View(permiso);
        }

        // GET: Permisos/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Permisos permiso = db.Permisos
                .Include(p => p.Empleados)
                .Include(p => p.Estados)
                .Include(p => p.TiposPermiso)
                .FirstOrDefault(p => p.id_permiso == id);

            if (permiso == null)
                return HttpNotFound();

            // Solo permitir eliminar permisos pendientes
            var estadoPendiente = db.Estados.FirstOrDefault(e => e.nombre == "Pendiente" && e.modulo == "Permisos");
            if (permiso.id_estado != estadoPendiente?.id_estado)
            {
                TempData["Error"] = "Solo se pueden eliminar permisos en estado Pendiente.";
                return RedirectToAction("Index");
            }

            return View(permiso);
        }

        // POST: Permisos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                Permisos permiso = db.Permisos.Find(id);
                if (permiso == null)
                    return HttpNotFound();

                // Verificar que solo se puedan eliminar permisos pendientes
                var estadoPendiente = db.Estados.FirstOrDefault(e => e.nombre == "Pendiente" && e.modulo == "Permisos");
                if (permiso.id_estado != estadoPendiente?.id_estado)
                {
                    TempData["Error"] = "Solo se pueden eliminar permisos en estado Pendiente.";
                    return RedirectToAction("Index");
                }

                db.Permisos.Remove(permiso);
                db.SaveChanges();

                var currentUserId = (int?)Session["UserId"];
                if (currentUserId!= null)
                {
                    BitacoraHelper.RegistrarAccion("ELIMINAR_PERMISO",
                        $"Eliminado permiso ID: {id}",
                        currentUserId.Value);
                }

                TempData["Success"] = "Permiso eliminado exitosamente.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar el permiso: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // Método para aprobar/rechazar permisos
        [HttpPost]
        public ActionResult CambiarEstado(int id, int nuevoEstado, string comentario = "")
        {
            try
            {
                var permiso = db.Permisos.Find(id);
                if (permiso == null)
                    return Json(new { success = false, message = "Permiso no encontrado." });

                // Validar que el estado existe y es válido para permisos
                var estado = db.Estados.FirstOrDefault(e => e.id_estado == nuevoEstado && e.modulo == "Permisos");
                if (estado == null)
                    return Json(new { success = false, message = "Estado no válido." });

                permiso.id_estado = nuevoEstado;
                permiso.aprobado_por = (int?)Session["UserId"];
                permiso.fecha_actualizacion = DateTime.Now;

                db.SaveChanges();

                var currentUserId = (int?)Session["UserId"];
                if (currentUserId!= null)
                {
                    BitacoraHelper.RegistrarAccion("CAMBIAR_ESTADO_PERMISO",
                        $"Permiso ID: {id} cambió a estado: {estado.nombre}. Comentario: {comentario}",
                        currentUserId.Value);
                }

                return Json(new { success = true, message = $"Permiso {estado.nombre.ToLower()} exitosamente." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        private void CargarListas(Permisos permiso = null)
        {
            ViewBag.id_empleado = new SelectList(
                db.Empleados.Where(e => e.estado == "ACTIVO")
                .Select(e => new {
                    id_empleado = e.id_empleado,
                    NombreCompleto = e.nombre1 + " " + e.apellido1 + " " + e.apellido2 + " (" + e.cedula + ")"
                }),
                "id_empleado", "NombreCompleto", permiso?.id_empleado);

            ViewBag.id_estado = new SelectList(
                db.Estados.Where(e => e.modulo == "Permisos"),
                "id_estado", "nombre", permiso?.id_estado);

            ViewBag.id_tipo_permiso = new SelectList(
                db.TiposPermiso.OrderBy(t => t.nombre),
                "id_tipo_permiso", "nombre", permiso?.id_tipo_permiso);

            // ✅ AGREGAR ESTA LÍNEA para los filtros
            ViewBag.TiposPermiso = db.TiposPermiso.OrderBy(t => t.nombre).ToList();

            ViewBag.aprobado_por = new SelectList(
                db.Usuarios.Where(u => u.primer_ingreso == true),
                "id_usuario", "usuario", permiso?.aprobado_por);
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