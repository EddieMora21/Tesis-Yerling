using System;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using SistemaNomina.Models;
using SistemaNomina.Filters;
using SistemaNomina.Helpers;

namespace SistemaNomina.Controllers
{
    [RoleAuthorize("Admin", "RRHH", "IT")]
    public class EstadosController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Lista de módulos válidos
        private readonly string[] ModulosValidos = { "Permisos", "Vacaciones", "Asistencias" };

        // Mostrar la lista de estados
        public ActionResult Index()
        {
            var estados = db.Estados.OrderBy(e => e.modulo).ThenBy(e => e.nombre).ToList();
            return View(estados);
        }

        // Mostrar detalles de un estado específico
        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Estados estado = db.Estados.Find(id);
            if (estado == null)
                return HttpNotFound();

            return View(estado);
        }

        // Mostrar formulario para crear nuevo estado
        public ActionResult Create()
        {
            ViewBag.Modulos = ModulosValidos.ToList();
            var model = new Estados();
            return View(model);
        }

        // Guardar nuevo estado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "nombre,modulo")] Estados estado)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // 🔍 VALIDACIÓN DE NEGOCIO - Módulo válido
                    if (!ModulosValidos.Contains(estado.modulo))
                    {
                        ModelState.AddModelError("modulo", "El módulo seleccionado no es válido.");
                        ViewBag.Modulos = ModulosValidos.ToList();
                        return View(estado);
                    }

                    // 🔍 VALIDACIÓN DE NEGOCIO - Nombre único por módulo
                    var existeEstado = db.Estados.Any(e => e.nombre.ToLower() == estado.nombre.ToLower()
                                                        && e.modulo == estado.modulo);
                    if (existeEstado)
                    {
                        ModelState.AddModelError("nombre", $"Ya existe un estado '{estado.nombre}' en el módulo '{estado.modulo}'.");
                        ViewBag.Modulos = ModulosValidos.ToList();
                        return View(estado);
                    }

                    // 📅 Fechas automáticas
                    estado.fecha_creacion = DateTime.Now;
                    estado.fecha_actualizacion = DateTime.Now;

                    db.Estados.Add(estado);
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Estado creado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("CREAR_ESTADO",
                            $"Creado estado: {estado.nombre} (Módulo: {estado.modulo})",
                            currentUserId.Value);
                    }

                    TempData["Success"] = $"Estado '{estado.nombre}' creado exitosamente para el módulo '{estado.modulo}'.";
                    return RedirectToAction("Index");
                }
            }
            catch (DbUpdateException ex)
            {
                HandleDbUpdateException(ex);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error inesperado: {ex.Message}");
                LogExceptionDetails(ex);
            }

            ViewBag.Modulos = ModulosValidos.ToList();
            return View(estado);
        }

        // Mostrar formulario para editar estado
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Estados estado = db.Estados.Find(id);
            if (estado == null)
                return HttpNotFound();

            ViewBag.Modulos = ModulosValidos.ToList();
            return View(estado);
        }

        // Guardar cambios en estado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_estado,nombre,modulo,fecha_creacion")] Estados estado)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // 🔍 VALIDACIÓN DE NEGOCIO - Módulo válido
                    if (!ModulosValidos.Contains(estado.modulo))
                    {
                        ModelState.AddModelError("modulo", "El módulo seleccionado no es válido.");
                        ViewBag.Modulos = ModulosValidos.ToList();
                        return View(estado);
                    }

                    // 🔍 VALIDACIÓN DE NEGOCIO - Nombre único por módulo (excluyendo el actual)
                    var existeEstado = db.Estados.Any(e => e.nombre.ToLower() == estado.nombre.ToLower()
                                                        && e.modulo == estado.modulo
                                                        && e.id_estado != estado.id_estado);
                    if (existeEstado)
                    {
                        ModelState.AddModelError("nombre", $"Ya existe otro estado '{estado.nombre}' en el módulo '{estado.modulo}'.");
                        ViewBag.Modulos = ModulosValidos.ToList();
                        return View(estado);
                    }

                    // 📅 Actualizar fecha de modificación
                    estado.fecha_actualizacion = DateTime.Now;

                    db.Entry(estado).State = EntityState.Modified;
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Estado editado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId.HasValue)
                    {
                        BitacoraHelper.RegistrarAccion("EDITAR_ESTADO",
                            $"Editado estado: {estado.nombre} (Módulo: {estado.modulo}) (ID: {estado.id_estado})",
                            currentUserId.Value);
                    }

                    TempData["Success"] = $"Estado '{estado.nombre}' actualizado exitosamente.";
                    return RedirectToAction("Index");
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError("", "Otro usuario modificó este registro. Recarga la página.");
            }
            catch (DbUpdateException ex)
            {
                HandleDbUpdateException(ex);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error inesperado: {ex.Message}");
                LogExceptionDetails(ex);
            }

            ViewBag.Modulos = ModulosValidos.ToList();
            return View(estado);
        }

        // Mostrar confirmación para eliminar estado
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Estados estado = db.Estados.Find(id);
            if (estado == null)
                return HttpNotFound();

            // 🔍 VALIDACIÓN DE NEGOCIO - Verificar registros asociados según módulo
            int registrosAsociados = 0;
            string tipoRegistros = "";

            switch (estado.modulo?.ToLower())
            {
                case "permisos":
                    registrosAsociados = db.Permisos.Count(p => p.id_estado == id);
                    tipoRegistros = "permiso(s)";
                    break;
                case "vacaciones":
                    registrosAsociados = db.SolicitudesVacaciones.Count(v => v.id_estado == id);
                    tipoRegistros = "solicitud(es) de vacaciones";
                    break;
                case "asistencias":
                    // Si hay tabla de asistencias con estados, agregar aquí
                    registrosAsociados = 0; // db.Asistencias.Count(a => a.id_estado == id);
                    tipoRegistros = "registro(s) de asistencia";
                    break;
            }

            ViewBag.RegistrosAsociados = registrosAsociados;
            ViewBag.TipoRegistros = tipoRegistros;

            return View(estado);
        }

        // Eliminar estado (con validaciones de negocio)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            try
            {
                Estados estado = db.Estados.Find(id);
                if (estado == null)
                    return HttpNotFound();

                // 🛡️ VALIDACIÓN DE NEGOCIO - No eliminar si hay registros asociados
                int registrosAsociados = 0;
                string tipoRegistros = "";

                switch (estado.modulo?.ToLower())
                {
                    case "permisos":
                        registrosAsociados = db.Permisos.Count(p => p.id_estado == id);
                        tipoRegistros = "permiso(s)";
                        break;
                    case "vacaciones":
                        registrosAsociados = db.SolicitudesVacaciones.Count(v => v.id_estado == id);
                        tipoRegistros = "solicitud(es) de vacaciones";
                        break;
                    case "asistencias":
                        // registrosAsociados = db.Asistencias.Count(a => a.id_estado == id);
                        tipoRegistros = "registro(s) de asistencia";
                        break;
                }

                if (registrosAsociados > 0)
                {
                    TempData["Error"] = $"No se puede eliminar el estado '{estado.nombre}' porque está asignado a {registrosAsociados} {tipoRegistros}.";
                    return RedirectToAction("Index");
                }

                // 📋 LOG AUTOMÁTICO - Estado eliminado
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId.HasValue)
                {
                    BitacoraHelper.RegistrarAccion("ELIMINAR_ESTADO",
                        $"Eliminado estado: {estado.nombre} (Módulo: {estado.modulo}) (ID: {estado.id_estado})",
                        currentUserId.Value);
                }

                db.Estados.Remove(estado);
                db.SaveChanges();

                TempData["Success"] = $"Estado '{estado.nombre}' eliminado exitosamente.";
            }
            catch (DbUpdateException ex)
            {
                HandleDbUpdateException(ex);
                TempData["Error"] = "Error al eliminar el estado. Verifique que no esté siendo utilizado.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error inesperado al eliminar: {ex.Message}";
                LogExceptionDetails(ex);
            }

            return RedirectToAction("Index");
        }

        // Limpiar recursos
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }

        #region Métodos auxiliares para manejo de errores

        // Método para manejar errores específicos de base de datos
        private void HandleDbUpdateException(DbUpdateException dbEx)
        {
            var innerException = dbEx.InnerException?.InnerException ?? dbEx.InnerException;

            if (innerException != null)
            {
                if (innerException.Message.Contains("FK_"))
                {
                    ModelState.AddModelError("", "No se puede realizar esta acción porque hay registros relacionados.");
                }
                else if (innerException.Message.Contains("IX_") || innerException.Message.Contains("UNIQUE"))
                {
                    ModelState.AddModelError("", "Ya existe un registro con estos valores (violación de restricción única).");
                }
                else
                {
                    ModelState.AddModelError("", $"Error de base de datos: {innerException.Message}");
                }
            }
            else
            {
                ModelState.AddModelError("", $"Error de base de datos: {dbEx.Message}");
            }
        }

        // Método para registrar detalles del error
        private void LogExceptionDetails(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en EstadosController: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }

        #endregion
    }
}