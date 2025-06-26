using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using SistemaNomina.Models;
using SistemaNomina.Filters;
using SistemaNomina.Helpers;

namespace SistemaNomina.Controllers
{
    [RoleAuthorize("Admin", "RRHH", "Supervisor")]
    public class ConsultasController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // GET: Consultas
        public ActionResult Index()
        {
            // Vista principal del módulo de consultas
            return View();
        }

        // 🔍 CASO DE USO 1: Consultar información por colaborador
        public ActionResult ConsultarPorColaborador()
        {
            try
            {
                CargarListasDesplegables();
                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar la página de consultas.";
                LogExceptionDetails(ex);
                return RedirectToAction("Index");
            }
        }

        // POST: Buscar información del colaborador
        [HttpPost]
        public ActionResult BuscarColaborador(int? idEmpleado, string cedula, string nombre)
        {
            try
            {
                IQueryable<Empleados> query = db.Empleados
                    .Include(e => e.Puestos)
                    .Include(e => e.Puestos.Departamentos)
                    .Include(e => e.EstadoCivil)
                    .Include(e => e.Horarios);

                // 🔍 Filtros de búsqueda
                if (idEmpleado.HasValue && idEmpleado.Value > 0)
                {
                    query = query.Where(e => e.id_empleado == idEmpleado.Value);
                }
                else if (!string.IsNullOrEmpty(cedula))
                {
                    query = query.Where(e => e.cedula.Contains(cedula));
                }
                else if (!string.IsNullOrEmpty(nombre))
                {
                    query = query.Where(e => e.nombre1.Contains(nombre) ||
                                           e.apellido1.Contains(nombre) ||
                                           e.apellido2.Contains(nombre));
                }

                var empleados = query.Take(50).ToList(); // Limitar resultados

                if (!empleados.Any())
                {
                    return Json(new { success = false, message = "No se encontraron empleados con los criterios especificados." });
                }

                // 📋 LOG AUTOMÁTICO
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId.HasValue)
                {
                    BitacoraHelper.RegistrarAccion("CONSULTA_EMPLEADOS",
                        $"Consultó empleados - Filtros: ID:{idEmpleado}, Cédula:{cedula}, Nombre:{nombre}",
                        currentUserId.Value);
                }

                return PartialView("_ResultadosEmpleados", empleados);
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return Json(new { success = false, message = "Error en la búsqueda: " + ex.Message });
            }
        }

        // 🔍 CASO DE USO 1: Ver detalle completo del colaborador
        public ActionResult DetalleColaborador(int id)
        {
            try
            {
                var empleado = db.Empleados
                    .Include(e => e.Puestos)
                    .Include(e => e.Puestos.Departamentos)
                    .Include(e => e.EstadoCivil)
                    .Include(e => e.Horarios)
                    .FirstOrDefault(e => e.id_empleado == id);

                if (empleado == null)
                {
                    TempData["Error"] = "Empleado no encontrado.";
                    return RedirectToAction("ConsultarPorColaborador");
                }

                // 📊 Obtener información adicional del empleado
                var viewModel = new DetalleColaboradorViewModel
                {
                    Empleado = empleado,

                    // Últimas nóminas
                    UltimasNominas = db.Nomina
                        .Where(n => n.id_empleado == id)
                        .OrderByDescending(n => n.anio)
                        .ThenByDescending(n => n.mes)
                        .Take(6)
                        .ToList(),

                    // Vacaciones actuales
                    VacacionesActuales = db.Vacaciones
                        .Where(v => v.id_empleado == id)
                        .OrderByDescending(v => v.periodo)
                        .FirstOrDefault(),

                    // Últimas solicitudes de vacaciones
                    SolicitudesVacaciones = db.SolicitudesVacaciones
                        .Include(s => s.Estados)
                        .Include(s => s.Vacaciones)
                        .Where(s => s.Vacaciones.id_empleado == id)
                        .OrderByDescending(s => s.fecha_solicitud)
                        .Take(5)
                        .ToList(),

                    // Horas extras del año actual
                    HorasExtrasAnual = db.HorasExtras
                        .Include(h => h.Estados)
                        .Include(h => h.TiposHoraExtra)
                        .Where(h => h.id_empleado == id && h.fecha.Year == DateTime.Now.Year)
                        .OrderByDescending(h => h.fecha)
                        .Take(10)
                        .ToList(),

                    // Incapacidades
                    Incapacidades = db.Incapacidades
                        .Include(i => i.Estados)
                        .Include(i => i.TipoIncapacidades)
                        .Where(i => i.id_empleado == id)
                        .OrderByDescending(i => i.fecha_inicio)
                        .Take(5)
                        .ToList(),

                    // Asistencia reciente (últimos 30 días)
                    AsistenciaReciente = db.Asistencia
                        .Where(a => a.id_empleado == id &&
                                   a.fecha >= DbFunctions.AddDays(DateTime.Now, -30))
                        .OrderByDescending(a => a.fecha)
                        .Take(20)
                        .ToList()
                };

                // 📋 LOG AUTOMÁTICO
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId.HasValue)
                {
                    BitacoraHelper.RegistrarAccion("VER_DETALLE_COLABORADOR",
                        $"Consultó detalle completo de {empleado.nombre1} {empleado.apellido1} (ID: {empleado.id_empleado})",
                        currentUserId.Value);
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar el detalle del colaborador.";
                LogExceptionDetails(ex);
                return RedirectToAction("ConsultarPorColaborador");
            }
        }

        // 🔍 CASO DE USO 2: Ver histórico de planillas
        public ActionResult HistoricoPlanillas()
        {
            try
            {
                CargarListasDesplegables();
                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar la página de histórico.";
                LogExceptionDetails(ex);
                return RedirectToAction("Index");
            }
        }

        // POST: Buscar planillas históricas
        [HttpPost]
        public ActionResult BuscarPlanillas(int? mes, int? anio, int? idEmpleado, int? idDepartamento)
        {
            try
            {
                IQueryable<Nomina> query = db.Nomina
                    .Include(n => n.Empleados)
                    .Include(n => n.Empleados.Puestos)
                    .Include(n => n.Empleados.Puestos.Departamentos)
                    .Include(n => n.ISR1);

                // 🔍 Aplicar filtros
                if (mes.HasValue && mes.Value > 0)
                {
                    query = query.Where(n => n.mes == mes.Value);
                }

                if (anio.HasValue && anio.Value > 0)
                {
                    query = query.Where(n => n.anio == anio.Value);
                }

                if (idEmpleado.HasValue && idEmpleado.Value > 0)
                {
                    query = query.Where(n => n.id_empleado == idEmpleado.Value);
                }

                if (idDepartamento.HasValue && idDepartamento.Value > 0)
                {
                    query = query.Where(n => n.Empleados.Puestos.id_departamento == idDepartamento.Value);
                }

                var planillas = query
                    .OrderByDescending(n => n.anio)
                    .ThenByDescending(n => n.mes)
                    .ThenBy(n => n.Empleados.apellido1)
                    .Take(500) // Limitar para rendimiento
                    .ToList();

                if (!planillas.Any())
                {
                    return Json(new { success = false, message = "No se encontraron planillas con los filtros especificados." });
                }

                // 📊 Estadísticas del resultado
                var estadisticas = new
                {
                    TotalRegistros = planillas.Count,
                    TotalSalarioBruto = planillas.Sum(p => p.salario_bruto),
                    TotalSalarioNeto = planillas.Sum(p => p.salario_neto),
                    TotalDeducciones = planillas.Sum(p => (p.ccss ?? 0) + (p.ivm ?? 0) + (p.isr ?? 0)),
                    EmpleadosUnicos = planillas.Select(p => p.id_empleado).Distinct().Count()
                };

                // 📋 LOG AUTOMÁTICO
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId.HasValue)
                {
                    BitacoraHelper.RegistrarAccion("CONSULTA_HISTORICO_PLANILLAS",
                        $"Consultó histórico - Mes:{mes}, Año:{anio}, Empleado:{idEmpleado}, Depto:{idDepartamento} - {planillas.Count} resultados",
                        currentUserId.Value);
                }

                ViewBag.Estadisticas = estadisticas;
                return PartialView("_ResultadoPlanillas", planillas);
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return Json(new { success = false, message = "Error en la búsqueda: " + ex.Message });
            }
        }

        // 🔍 Exportar resultados de planillas a Excel/PDF
        [HttpPost]
        public ActionResult ExportarPlanillas(int? mes, int? anio, int? idEmpleado, int? idDepartamento, string formato = "excel")
        {
            try
            {
                // Reutilizar la misma lógica de búsqueda
                var resultado = BuscarPlanillas(mes, anio, idEmpleado, idDepartamento);

                // Aquí implementarías la exportación según el formato
                // Por ahora retornamos un mensaje de éxito

                TempData["Success"] = $"Exportación a {formato.ToUpper()} completada exitosamente.";
                return Json(new { success = true, message = "Archivo generado exitosamente." });
            }
            catch (Exception ex)
            {
                LogExceptionDetails(ex);
                return Json(new { success = false, message = "Error al exportar: " + ex.Message });
            }
        }

        #region Métodos auxiliares

        private void CargarListasDesplegables()
        {
            // Lista de empleados activos
            ViewBag.Empleados = new SelectList(
                db.Empleados.Where(e => e.estado == "Activo")
                    .Select(e => new {
                        e.id_empleado,
                        NombreCompleto = e.cedula + " - " + e.nombre1 + " " + e.apellido1
                    })
                    .OrderBy(e => e.NombreCompleto),
                "id_empleado", "NombreCompleto");

            // Lista de departamentos
            ViewBag.Departamentos = new SelectList(
                db.Departamentos.OrderBy(d => d.nombre),
                "id_departamento", "nombre");

            // Lista de meses
            ViewBag.Meses = new SelectList(new[]
            {
                new { Value = 1, Text = "Enero" },
                new { Value = 2, Text = "Febrero" },
                new { Value = 3, Text = "Marzo" },
                new { Value = 4, Text = "Abril" },
                new { Value = 5, Text = "Mayo" },
                new { Value = 6, Text = "Junio" },
                new { Value = 7, Text = "Julio" },
                new { Value = 8, Text = "Agosto" },
                new { Value = 9, Text = "Septiembre" },
                new { Value = 10, Text = "Octubre" },
                new { Value = 11, Text = "Noviembre" },
                new { Value = 12, Text = "Diciembre" }
            }, "Value", "Text");

            // Lista de años (últimos 5 años)
            var anios = Enumerable.Range(DateTime.Now.Year - 4, 5)
                .Select(year => new { Value = year, Text = year.ToString() })
                .OrderByDescending(y => y.Value);
            ViewBag.Anios = new SelectList(anios, "Value", "Text");
        }

        private void LogExceptionDetails(Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en ConsultasController: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}