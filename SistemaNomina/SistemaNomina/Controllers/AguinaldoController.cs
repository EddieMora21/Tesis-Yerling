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
    public class AguinaldoController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // GET: Aguinaldo
        public ActionResult Index()
        {
            var aguinaldo = db.Aguinaldo.Include(a => a.Empleados)
                .OrderByDescending(a => a.anio)
                .ThenBy(a => a.Empleados.apellido1);
            return View(aguinaldo.ToList());
        }

        // GET: Aguinaldo/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Aguinaldo aguinaldo = db.Aguinaldo.Find(id);
            if (aguinaldo == null)
            {
                return HttpNotFound();
            }
            return View(aguinaldo);
        }

        // GET: Aguinaldo/CalcularAguinaldo - NUEVO MÉTODO PRINCIPAL
        public ActionResult CalcularAguinaldo()
        {
            // Cargar empleados activos para selección
            ViewBag.EmpleadosActivos = db.Empleados
                .Where(e => e.estado == "Activo")
                .Select(e => new SelectListItem
                {
                    Value = e.id_empleado.ToString(),
                    Text = e.cedula + " - " + e.nombre1 + " " + e.apellido1
                }).ToList();

            // Año actual por defecto
            ViewBag.AnioActual = DateTime.Now.Year;

            return View();
        }

        // POST: Calcular aguinaldo automáticamente
        [HttpPost]
        
        public ActionResult CalcularAguinaldoAutomatico(int idEmpleado, int anio)
        {
            try
            {
                // VALIDACIÓN: No permitir años muy futuros
                if (anio > DateTime.Now.Year + 1)
                {
                    return Json(new { success = false, message = "No se puede calcular aguinaldo para años tan futuros" });
                }

                var empleado = db.Empleados.Find(idEmpleado);
                if (empleado == null)
                {
                    return Json(new { success = false, message = "Empleado no encontrado" });
                }

                // Calcular período de aguinaldo (1 dic año anterior al 30 nov año actual)
                DateTime fechaInicio = new DateTime(anio - 1, 12, 1);
                DateTime fechaFin = new DateTime(anio, 11, 30);

                // VALIDACIÓN: Si es año futuro, ajustar fecha fin a hoy
                if (fechaFin > DateTime.Today)
                {
                    fechaFin = DateTime.Today;
                }

                // Verificar que el empleado haya trabajado en el período
                if (empleado.fecha_ingreso > fechaFin)
                {
                    return Json(new { success = false, message = "El empleado ingresó después del período de aguinaldo" });
                }

                // Ajustar fechas si el empleado ingresó durante el período
                if (empleado.fecha_ingreso > fechaInicio)
                {
                    fechaInicio = empleado.fecha_ingreso;
                }

                // Obtener salarios del período desde la tabla Nomina
                var salariosDelPeriodo = db.Nomina
                    .Where(n => n.id_empleado == idEmpleado &&
                               ((n.anio == anio - 1 && n.mes == 12) ||
                                (n.anio == anio && n.mes <= 11)))
                    .ToList();

                decimal sumaSalarios = salariosDelPeriodo.Sum(s => s.salario_bruto);
                int mesesLaborados = salariosDelPeriodo.Count();

                // Si no hay registros en nómina, calcular con salario base
                if (mesesLaborados == 0)
                {
                    var puestoEmpleado = db.Puestos.Find(empleado.id_puesto);
                    if (puestoEmpleado != null)
                    {
                        // Calcular meses trabajados manualmente
                        DateTime fechaCalculoInicio = empleado.fecha_ingreso > fechaInicio ? empleado.fecha_ingreso : fechaInicio;
                        TimeSpan diferencia = fechaFin - fechaCalculoInicio;
                        mesesLaborados = Math.Max(1, (int)Math.Ceiling(diferencia.TotalDays / 30));
                        sumaSalarios = puestoEmpleado.salario_base * mesesLaborados;
                    }
                }

                // Considerar ausencias injustificadas
                var ajustePorAusencias = ConsiderarAusenciasEnCalculo(idEmpleado, fechaInicio, fechaFin);
                sumaSalarios -= ajustePorAusencias.montoDescontado;

                // Aplicar fórmula legal: (Suma de salarios devengados / 12)
                decimal montoAguinaldo = sumaSalarios / 12m;

                // Verificar si ya existe aguinaldo para este empleado y año
                var aguinaldoExistente = db.Aguinaldo
                    .FirstOrDefault(a => a.id_empleado == idEmpleado && a.anio == anio);

                var resultado = new
                {
                    success = true,
                    empleado = empleado.nombre1 + " " + empleado.apellido1,
                    cedula = empleado.cedula,
                    fechaIngreso = empleado.fecha_ingreso.ToString("dd/MM/yyyy"),
                    periodoInicio = fechaInicio.ToString("dd/MM/yyyy"),
                    periodoFin = fechaFin.ToString("dd/MM/yyyy"),
                    mesesLaborados = mesesLaborados,
                    sumaSalarios = sumaSalarios.ToString("C"),
                    montoAguinaldo = montoAguinaldo.ToString("C"),
                    diasAusencias = ajustePorAusencias.diasDescontados,
                    montoDescontadoAusencias = ajustePorAusencias.montoDescontado.ToString("C"),
                    aguinaldoExistente = aguinaldoExistente != null,
                    detalleCalculo = new
                    {
                        salariosIncluidos = salariosDelPeriodo.Select(s => new
                        {
                            mes = s.mes,
                            anio = s.anio,
                            salario = s.salario_bruto.ToString("C")
                        }).ToList()
                    }
                };

                return Json(resultado);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error en el cálculo: " + ex.Message });
            }
        }

        // Método auxiliar para considerar ausencias injustificadas
        // Método auxiliar para considerar ausencias injustificadas - CORREGIDO
        private (int diasDescontados, decimal montoDescontado) ConsiderarAusenciasEnCalculo(int idEmpleado, DateTime fechaInicio, DateTime fechaFin)
        {
            try
            {
                // CORREGIR: Usar DbFunctions.TruncateTime en lugar de .Date
                var diasConAsistencia = db.Asistencia
                    .Where(a => a.id_empleado == idEmpleado &&
                               a.fecha >= fechaInicio &&
                               a.fecha <= fechaFin &&
                               a.hora_entrada != null)
                    .Select(a => a.fecha)
                    .ToList() // Materializar la consulta primero
                    .Select(f => f.Date) // Ahora aplicar .Date en memoria
                    .ToList();

                var incapacidades = db.Incapacidades
                    .Where(i => i.id_empleado == idEmpleado &&
                               i.fecha_inicio <= fechaFin &&
                               i.fecha_fin >= fechaInicio)
                    .ToList(); // Materializar antes de procesar

                var permisosAprobados = db.Permisos
                    .Where(p => p.id_empleado == idEmpleado &&
                               p.fecha >= fechaInicio &&
                               p.fecha <= fechaFin &&
                               p.Estados.nombre == "Aprobado")
                    .Select(p => p.fecha)
                    .ToList() // Materializar la consulta primero
                    .Select(f => f.Date) // Ahora aplicar .Date en memoria
                    .ToList();

                // Calcular días laborales en el período (excluyendo fines de semana)
                var diasLaborales = new List<DateTime>();
                for (DateTime fecha = fechaInicio; fecha <= fechaFin; fecha = fecha.AddDays(1))
                {
                    if (fecha.DayOfWeek != DayOfWeek.Saturday && fecha.DayOfWeek != DayOfWeek.Sunday)
                    {
                        diasLaborales.Add(fecha.Date);
                    }
                }

                // Días que deberían tener asistencia pero no la tienen
                var diasAusencias = diasLaborales
                    .Where(d => !diasConAsistencia.Contains(d) &&
                               !permisosAprobados.Contains(d) &&
                               !incapacidades.Any(inc => d >= inc.fecha_inicio.Date && d <= inc.fecha_fin.Date))
                    .ToList();

                // Calcular monto descontado basado en salario diario
                var empleado = db.Empleados.Find(idEmpleado);
                if (empleado?.Puestos?.salario_base == null)
                {
                    return (0, 0);
                }

                decimal salarioDiario = empleado.Puestos.salario_base / 30m;
                decimal montoDescontado = diasAusencias.Count * salarioDiario;

                return (diasAusencias.Count, montoDescontado);
            }
            catch (Exception ex)
            {
                // Log del error para debugging
                System.Diagnostics.Debug.WriteLine($"Error en ConsiderarAusenciasEnCalculo: {ex.Message}");
                return (0, 0); // Devolver valores seguros en caso de error
            }
        }

        // POST: Generar aguinaldo para todos los empleados
        [HttpPost]
        public ActionResult GenerarParaTodosLosEmpleados(int anio)
        {
            try
            {
                var empleadosActivos = db.Empleados.Where(e => e.estado == "Activo").ToList();
                var resultados = new List<object>();
                int exitosos = 0;
                int errores = 0;

                foreach (var empleado in empleadosActivos)
                {
                    try
                    {
                        // Verificar si ya existe aguinaldo para este empleado y año
                        var aguinaldoExistente = db.Aguinaldo
                            .FirstOrDefault(a => a.id_empleado == empleado.id_empleado && a.anio == anio);

                        if (aguinaldoExistente == null)
                        {
                            // Calcular aguinaldo para este empleado
                            DateTime fechaInicio = new DateTime(anio - 1, 12, 1);
                            DateTime fechaFin = new DateTime(anio, 11, 30);

                            if (empleado.fecha_ingreso <= fechaFin)
                            {
                                if (empleado.fecha_ingreso > fechaInicio)
                                {
                                    fechaInicio = empleado.fecha_ingreso;
                                }

                                var salariosDelPeriodo = db.Nomina
                                    .Where(n => n.id_empleado == empleado.id_empleado &&
                                               ((n.anio == anio - 1 && n.mes == 12) ||
                                                (n.anio == anio && n.mes <= 11)))
                                    .ToList();

                                decimal sumaSalarios = salariosDelPeriodo.Sum(s => s.salario_bruto);
                                int mesesLaborados = salariosDelPeriodo.Count();

                                if (mesesLaborados == 0)
                                {
                                    var puestoEmpleado = db.Puestos.Find(empleado.id_puesto);
                                    if (puestoEmpleado != null)
                                    {
                                        DateTime fechaCalculoInicio = empleado.fecha_ingreso > fechaInicio ? empleado.fecha_ingreso : fechaInicio;
                                        TimeSpan diferencia = fechaFin - fechaCalculoInicio;
                                        mesesLaborados = Math.Max(1, (int)Math.Ceiling(diferencia.TotalDays / 30));
                                        sumaSalarios = puestoEmpleado.salario_base * mesesLaborados;
                                    }
                                }

                                var ajustePorAusencias = ConsiderarAusenciasEnCalculo(empleado.id_empleado, fechaInicio, fechaFin);
                                sumaSalarios -= ajustePorAusencias.montoDescontado;

                                decimal montoAguinaldo = sumaSalarios / 12m;

                                var nuevoAguinaldo = new Aguinaldo
                                {
                                    id_empleado = empleado.id_empleado,
                                    monto_total = montoAguinaldo,
                                    meses_laborados = mesesLaborados,
                                    anio = anio,
                                    fecha_creacion = DateTime.Now
                                };

                                db.Aguinaldo.Add(nuevoAguinaldo);
                                exitosos++;

                                resultados.Add(new
                                {
                                    empleado = empleado.nombre1 + " " + empleado.apellido1,
                                    cedula = empleado.cedula,
                                    monto = montoAguinaldo.ToString("C"),
                                    estado = "Creado"
                                });
                            }
                        }
                        else
                        {
                            resultados.Add(new
                            {
                                empleado = empleado.nombre1 + " " + empleado.apellido1,
                                cedula = empleado.cedula,
                                monto = aguinaldoExistente.monto_total.ToString("C"),
                                estado = "Ya existía"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        errores++;
                        resultados.Add(new
                        {
                            empleado = empleado.nombre1 + " " + empleado.apellido1,
                            cedula = empleado.cedula,
                            monto = "Error",
                            estado = "Error: " + ex.Message
                        });
                    }
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    exitosos = exitosos,
                    errores = errores,
                    total = empleadosActivos.Count,
                    resultados = resultados
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error general: " + ex.Message });
            }
        }

        // GET: Aguinaldo/Create
        public ActionResult Create()
        {
            ViewBag.id_empleado = new SelectList(db.Empleados.Where(e => e.estado == "Activo"), "id_empleado", "cedula");
            ViewBag.AnioActual = DateTime.Now.Year;
            return View();
        }

        // POST: Aguinaldo/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "id_aguinaldo,id_empleado,monto_total,meses_laborados,anio")] Aguinaldo aguinaldo)
        {
            if (ModelState.IsValid)
            {
                // Verificar que no exista ya un aguinaldo para este empleado y año
                var aguinaldoExistente = db.Aguinaldo
                    .FirstOrDefault(a => a.id_empleado == aguinaldo.id_empleado && a.anio == aguinaldo.anio);

                if (aguinaldoExistente != null)
                {
                    ModelState.AddModelError("", "Ya existe un aguinaldo registrado para este empleado en el año " + aguinaldo.anio);
                    ViewBag.id_empleado = new SelectList(db.Empleados.Where(e => e.estado == "Activo"), "id_empleado", "cedula", aguinaldo.id_empleado);
                    return View(aguinaldo);
                }

                aguinaldo.fecha_creacion = DateTime.Now;
                db.Aguinaldo.Add(aguinaldo);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.id_empleado = new SelectList(db.Empleados.Where(e => e.estado == "Activo"), "id_empleado", "cedula", aguinaldo.id_empleado);
            return View(aguinaldo);
        }

        // GET: Aguinaldo/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Aguinaldo aguinaldo = db.Aguinaldo.Find(id);
            if (aguinaldo == null)
            {
                return HttpNotFound();
            }
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", aguinaldo.id_empleado);
            return View(aguinaldo);
        }

        // POST: Aguinaldo/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_aguinaldo,id_empleado,monto_total,meses_laborados,anio,fecha_creacion")] Aguinaldo aguinaldo)
        {
            if (ModelState.IsValid)
            {
                aguinaldo.fecha_actualizacion = DateTime.Now;
                db.Entry(aguinaldo).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.id_empleado = new SelectList(db.Empleados, "id_empleado", "cedula", aguinaldo.id_empleado);
            return View(aguinaldo);
        }

        // GET: Aguinaldo/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Aguinaldo aguinaldo = db.Aguinaldo.Find(id);
            if (aguinaldo == null)
            {
                return HttpNotFound();
            }
            return View(aguinaldo);
        }

        // POST: Aguinaldo/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            Aguinaldo aguinaldo = db.Aguinaldo.Find(id);
            db.Aguinaldo.Remove(aguinaldo);
            db.SaveChanges();
            return RedirectToAction("Index");
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