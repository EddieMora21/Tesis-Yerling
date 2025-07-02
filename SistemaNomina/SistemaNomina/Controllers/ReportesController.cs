using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using SistemaNomina.Models;
using System.Data.Entity;

namespace SistemaNomina.Controllers
{
    public class ReportesController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // GET: Reportes
        public ActionResult Index()
        {
            try
            {
                // Cargar datos para los dropdowns
                ViewBag.Empleados = new SelectList(
                    db.Empleados.Where(e => e.estado == "Activo")
                              .Select(e => new {
                                  id_empleado = e.id_empleado,
                                  nombre_completo = e.nombre1 + " " + e.apellido1
                              }).ToList(),
                    "id_empleado",
                    "nombre_completo"
                );

                ViewBag.Departamentos = new SelectList(db.Departamentos.ToList(), "id_departamento", "nombre");
                ViewBag.TiposHoraExtra = new SelectList(db.TiposHoraExtra.ToList(), "id_tipo", "nombre");
                ViewBag.TiposIncapacidad = new SelectList(db.TipoIncapacidades.ToList(), "id_tipo", "nombre");

                ViewBag.Anios = Enumerable.Range(DateTime.Now.Year - 5, 6).Select(x => new SelectListItem
                {
                    Value = x.ToString(),
                    Text = x.ToString(),
                    Selected = x == DateTime.Now.Year
                }).ToList();

                ViewBag.Meses = Enumerable.Range(1, 12).Select(x => new SelectListItem
                {
                    Value = x.ToString(),
                    Text = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(x),
                    Selected = x == DateTime.Now.Month
                }).ToList();

                return View();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar la página de reportes: " + ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: Reportes/ReporteNomina
        [HttpPost]
        public ActionResult ReporteNomina(int? empleadoId, int? departamentoId, int mes, int anio)
        {
            try
            {
                var query = db.Nomina.Include(n => n.Empleados)
                                     .Include(n => n.Empleados.Puestos)
                                     .Include(n => n.Empleados.Puestos.Departamentos)
                                     .Where(n => n.mes == mes && n.anio == anio);

                if (empleadoId.HasValue)
                {
                    query = query.Where(n => n.id_empleado == empleadoId.Value);
                }

                if (departamentoId.HasValue)
                {
                    query = query.Where(n => n.Empleados.Puestos.id_departamento == departamentoId.Value);
                }

                var reporteData = query.ToList();

                ViewBag.Mes = mes;
                ViewBag.Anio = anio;
                ViewBag.NombreMes = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(mes);

                if (empleadoId.HasValue)
                {
                    var empleado = db.Empleados.Find(empleadoId.Value);
                    ViewBag.EmpleadoSeleccionado = empleado != null ? empleado.nombre1 + " " + empleado.apellido1 : "No encontrado";
                }
                else
                {
                    ViewBag.EmpleadoSeleccionado = "Todos";
                }

                if (departamentoId.HasValue)
                {
                    var departamento = db.Departamentos.Find(departamentoId.Value);
                    ViewBag.DepartamentoSeleccionado = departamento != null ? departamento.nombre : "No encontrado";
                }
                else
                {
                    ViewBag.DepartamentoSeleccionado = "Todos";
                }

                return View("ReporteNomina", reporteData);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al generar el reporte de nómina: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Reportes/ReporteAsistencia
        [HttpPost]
        public ActionResult ReporteAsistencia(int? empleadoId, DateTime fechaInicio, DateTime fechaFin)
        {
            try
            {
                var query = db.Asistencia.Include(a => a.Empleados)
                                        .Include(a => a.Empleados.Puestos)
                                        .Include(a => a.Empleados.Puestos.Departamentos)
                                        .Where(a => a.fecha >= fechaInicio && a.fecha <= fechaFin);

                if (empleadoId.HasValue)
                {
                    query = query.Where(a => a.id_empleado == empleadoId.Value);
                }

                var reporteData = query.OrderBy(a => a.fecha).ToList();

                ViewBag.FechaInicio = fechaInicio.ToString("dd/MM/yyyy");
                ViewBag.FechaFin = fechaFin.ToString("dd/MM/yyyy");

                if (empleadoId.HasValue)
                {
                    var empleado = db.Empleados.Find(empleadoId.Value);
                    ViewBag.EmpleadoSeleccionado = empleado != null ? empleado.nombre1 + " " + empleado.apellido1 : "No encontrado";
                }
                else
                {
                    ViewBag.EmpleadoSeleccionado = "Todos";
                }

                return View("ReporteAsistencia", reporteData);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al generar el reporte de asistencia: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Reportes/ReporteVacaciones
        [HttpPost]
        public ActionResult ReporteVacaciones(int? empleadoId, int? departamentoId, string periodo)
        {
            try
            {
                var query = db.Vacaciones.Include(v => v.Empleados)
                                        .Include(v => v.Empleados.Puestos)
                                        .Include(v => v.Empleados.Puestos.Departamentos)
                                        .Include(v => v.SolicitudesVacaciones)
                                        .Include(v => v.SolicitudesVacaciones.Select(s => s.Estados));

                if (!string.IsNullOrEmpty(periodo))
                {
                    query = query.Where(v => v.periodo == periodo);
                }

                if (empleadoId.HasValue)
                {
                    query = query.Where(v => v.id_empleado == empleadoId.Value);
                }

                if (departamentoId.HasValue)
                {
                    query = query.Where(v => v.Empleados.Puestos.id_departamento == departamentoId.Value);
                }

                var reporteData = query.ToList();

                ViewBag.Periodo = periodo ?? "Todos";

                if (empleadoId.HasValue)
                {
                    var empleado = db.Empleados.Find(empleadoId.Value);
                    ViewBag.EmpleadoSeleccionado = empleado != null ? empleado.nombre1 + " " + empleado.apellido1 : "No encontrado";
                }
                else
                {
                    ViewBag.EmpleadoSeleccionado = "Todos";
                }

                if (departamentoId.HasValue)
                {
                    var departamento = db.Departamentos.Find(departamentoId.Value);
                    ViewBag.DepartamentoSeleccionado = departamento != null ? departamento.nombre : "No encontrado";
                }
                else
                {
                    ViewBag.DepartamentoSeleccionado = "Todos";
                }

                return View("ReporteVacaciones", reporteData);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al generar el reporte de vacaciones: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Reportes/ReporteHorasExtras
        [HttpPost]
        public ActionResult ReporteHorasExtras(int? empleadoId, DateTime fechaInicio, DateTime fechaFin, int? tipoHoraExtra)
        {
            try
            {
                var query = db.HorasExtras.Include(h => h.Empleados)
                                         .Include(h => h.Empleados.Puestos)
                                         .Include(h => h.Empleados.Puestos.Departamentos)
                                         .Include(h => h.TiposHoraExtra)
                                         .Include(h => h.Estados)
                                         .Where(h => h.fecha >= fechaInicio && h.fecha <= fechaFin);

                if (empleadoId.HasValue)
                {
                    query = query.Where(h => h.id_empleado == empleadoId.Value);
                }

                if (tipoHoraExtra.HasValue)
                {
                    query = query.Where(h => h.id_tipo == tipoHoraExtra.Value);
                }

                var reporteData = query.OrderBy(h => h.fecha).ToList();

                ViewBag.FechaInicio = fechaInicio.ToString("dd/MM/yyyy");
                ViewBag.FechaFin = fechaFin.ToString("dd/MM/yyyy");

                if (empleadoId.HasValue)
                {
                    var empleado = db.Empleados.Find(empleadoId.Value);
                    ViewBag.EmpleadoSeleccionado = empleado != null ? empleado.nombre1 + " " + empleado.apellido1 : "No encontrado";
                }
                else
                {
                    ViewBag.EmpleadoSeleccionado = "Todos";
                }

                if (tipoHoraExtra.HasValue)
                {
                    var tipo = db.TiposHoraExtra.Find(tipoHoraExtra.Value);
                    ViewBag.TipoSeleccionado = tipo != null ? tipo.nombre : "No encontrado";
                }
                else
                {
                    ViewBag.TipoSeleccionado = "Todos";
                }

                return View("ReporteHorasExtras", reporteData);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al generar el reporte de horas extras: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // POST: Reportes/ReporteIncapacidades
        [HttpPost]
        public ActionResult ReporteIncapacidades(int? empleadoId, DateTime fechaInicio, DateTime fechaFin, int? tipoIncapacidad)
        {
            try
            {
                var query = db.Incapacidades.Include(i => i.Empleados)
                                           .Include(i => i.Empleados.Puestos)
                                           .Include(i => i.Empleados.Puestos.Departamentos)
                                           .Include(i => i.TipoIncapacidades)
                                           .Include(i => i.Estados)
                                           .Where(i => i.fecha_inicio >= fechaInicio && i.fecha_fin <= fechaFin);

                if (empleadoId.HasValue)
                {
                    query = query.Where(i => i.id_empleado == empleadoId.Value);
                }

                if (tipoIncapacidad.HasValue)
                {
                    query = query.Where(i => i.id_tipo_incapacidad == tipoIncapacidad.Value);
                }

                var reporteData = query.OrderBy(i => i.fecha_inicio).ToList();

                ViewBag.FechaInicio = fechaInicio.ToString("dd/MM/yyyy");
                ViewBag.FechaFin = fechaFin.ToString("dd/MM/yyyy");

                if (empleadoId.HasValue)
                {
                    var empleado = db.Empleados.Find(empleadoId.Value);
                    ViewBag.EmpleadoSeleccionado = empleado != null ? empleado.nombre1 + " " + empleado.apellido1 : "No encontrado";
                }
                else
                {
                    ViewBag.EmpleadoSeleccionado = "Todos";
                }

                if (tipoIncapacidad.HasValue)
                {
                    var tipo = db.TipoIncapacidades.Find(tipoIncapacidad.Value);
                    ViewBag.TipoSeleccionado = tipo != null ? tipo.nombre : "No encontrado";
                }
                else
                {
                    ViewBag.TipoSeleccionado = "Todos";
                }

                return View("ReporteIncapacidades", reporteData);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al generar el reporte de incapacidades: " + ex.Message;
                return RedirectToAction("Index");
            }
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