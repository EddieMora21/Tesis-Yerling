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

        // GET: Aguinaldo/CalcularAguinaldo - MÉTODO PRINCIPAL
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

        // POST: Calcular aguinaldo automáticamente - MEJORADO
        [HttpPost]
        public ActionResult CalcularAguinaldoAutomatico(int idEmpleado, int anio)
        {
            try
            {
                // VALIDACIONES MEJORADAS
                if (anio > DateTime.Now.Year + 1)
                {
                    return Json(new { success = false, message = "No se puede calcular aguinaldo para años tan futuros" });
                }

                if (anio < 2020)
                {
                    return Json(new { success = false, message = "Año muy antiguo para el cálculo" });
                }

                var empleado = db.Empleados.Include(e => e.Puestos).FirstOrDefault(e => e.id_empleado == idEmpleado);
                if (empleado == null)
                {
                    return Json(new { success = false, message = "Empleado no encontrado" });
                }

                // LOGGING PARA DEBUG
                System.Diagnostics.Debug.WriteLine($"=== CALCULANDO AGUINALDO ===");
                System.Diagnostics.Debug.WriteLine($"Empleado: {empleado.nombre1} {empleado.apellido1}");
                System.Diagnostics.Debug.WriteLine($"Año: {anio}");
                System.Diagnostics.Debug.WriteLine($"Salario Base Puesto: {empleado.Puestos?.salario_base}");

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

                System.Diagnostics.Debug.WriteLine($"Período: {fechaInicio:yyyy-MM-dd} a {fechaFin:yyyy-MM-dd}");

                // OBTENER SALARIOS DEL PERÍODO DESDE LA TABLA NOMINA
                var salariosDelPeriodo = db.Nomina
                    .Where(n => n.id_empleado == idEmpleado &&
                               ((n.anio == anio - 1 && n.mes == 12) ||
                                (n.anio == anio && n.mes <= 11)))
                    .OrderBy(n => n.anio).ThenBy(n => n.mes)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Registros de nómina encontrados: {salariosDelPeriodo.Count}");

                decimal sumaSalarios = 0;
                int mesesLaborados = 0;

                if (salariosDelPeriodo.Any())
                {
                    // USAR DATOS REALES DE NÓMINA
                    sumaSalarios = salariosDelPeriodo.Sum(s => s.salario_bruto);
                    mesesLaborados = salariosDelPeriodo.Count();
                    System.Diagnostics.Debug.WriteLine($"Usando datos de nómina - Suma: {sumaSalarios}, Meses: {mesesLaborados}");
                }
                else
                {
                    // FALLBACK: USAR SALARIO BASE DEL PUESTO
                    if (empleado.Puestos != null)
                    {
                        // Calcular meses trabajados manualmente
                        DateTime fechaCalculoInicio = empleado.fecha_ingreso > fechaInicio ? empleado.fecha_ingreso : fechaInicio;

                        // Calcular meses completos entre fechas
                        mesesLaborados = CalcularMesesCompletos(fechaCalculoInicio, fechaFin);
                        sumaSalarios = empleado.Puestos.salario_base * mesesLaborados;

                        System.Diagnostics.Debug.WriteLine($"Usando salario base - Fecha inicio: {fechaCalculoInicio:yyyy-MM-dd}");
                        System.Diagnostics.Debug.WriteLine($"Meses calculados: {mesesLaborados}");
                        System.Diagnostics.Debug.WriteLine($"Salario base: {empleado.Puestos.salario_base}");
                        System.Diagnostics.Debug.WriteLine($"Suma total: {sumaSalarios}");
                    }
                    else
                    {
                        return Json(new { success = false, message = "No se encontró información salarial para el empleado" });
                    }
                }

                // Considerar ausencias injustificadas
                var ajustePorAusencias = ConsiderarAusenciasEnCalculo(idEmpleado, fechaInicio, fechaFin);
                sumaSalarios -= ajustePorAusencias.montoDescontado;

                System.Diagnostics.Debug.WriteLine($"Ajuste por ausencias: -{ajustePorAusencias.montoDescontado}");
                System.Diagnostics.Debug.WriteLine($"Suma final: {sumaSalarios}");

                // APLICAR FÓRMULA LEGAL: (Suma de salarios devengados / 12)
                decimal montoAguinaldo = sumaSalarios / 12m;

                System.Diagnostics.Debug.WriteLine($"Aguinaldo calculado: {montoAguinaldo}");

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
                        }).ToList(),
                        usandoSalarioBase = !salariosDelPeriodo.Any(),
                        salarioBasePuesto = empleado.Puestos?.salario_base.ToString("C")
                    }
                };

                return Json(resultado);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR en CalcularAguinaldoAutomatico: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = "Error en el cálculo: " + ex.Message });
            }
        }

        // MÉTODO AUXILIAR MEJORADO: Calcular meses completos entre fechas
        private int CalcularMesesCompletos(DateTime fechaInicio, DateTime fechaFin)
        {
            int meses = 0;
            DateTime fechaTemp = new DateTime(fechaInicio.Year, fechaInicio.Month, 1);
            DateTime fechaFinMes = new DateTime(fechaFin.Year, fechaFin.Month, 1);

            while (fechaTemp <= fechaFinMes)
            {
                meses++;
                fechaTemp = fechaTemp.AddMonths(1);
            }

            return Math.Max(1, meses); // Mínimo 1 mes
        }

        // MÉTODO AUXILIAR CORREGIDO: Considerar ausencias injustificadas
        private (int diasDescontados, decimal montoDescontado) ConsiderarAusenciasEnCalculo(int idEmpleado, DateTime fechaInicio, DateTime fechaFin)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== CALCULANDO AUSENCIAS ===");
                System.Diagnostics.Debug.WriteLine($"Empleado ID: {idEmpleado}");
                System.Diagnostics.Debug.WriteLine($"Período: {fechaInicio:yyyy-MM-dd} a {fechaFin:yyyy-MM-dd}");

                // Obtener días con asistencia registrada
                var diasConAsistencia = db.Asistencia
                    .Where(a => a.id_empleado == idEmpleado &&
                               a.fecha >= fechaInicio &&
                               a.fecha <= fechaFin &&
                               a.hora_entrada != null)
                    .Select(a => a.fecha)
                    .ToList()
                    .Select(f => f.Date)
                    .Distinct()
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Días con asistencia: {diasConAsistencia.Count}");

                // Obtener incapacidades
                var incapacidades = db.Incapacidades
                    .Where(i => i.id_empleado == idEmpleado &&
                               i.fecha_inicio <= fechaFin &&
                               i.fecha_fin >= fechaInicio)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Incapacidades: {incapacidades.Count}");

                // Obtener permisos aprobados
                var permisosAprobados = db.Permisos
                    .Where(p => p.id_empleado == idEmpleado &&
                               p.fecha >= fechaInicio &&
                               p.fecha <= fechaFin &&
                               p.Estados.nombre == "Aprobado")
                    .Select(p => p.fecha)
                    .ToList()
                    .Select(f => f.Date)
                    .Distinct()
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Permisos aprobados: {permisosAprobados.Count}");

                // Calcular días laborales en el período (excluyendo fines de semana)
                var diasLaborales = new List<DateTime>();
                for (DateTime fecha = fechaInicio; fecha <= fechaFin; fecha = fecha.AddDays(1))
                {
                    if (fecha.DayOfWeek != DayOfWeek.Saturday && fecha.DayOfWeek != DayOfWeek.Sunday)
                    {
                        diasLaborales.Add(fecha.Date);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Días laborales totales: {diasLaborales.Count}");

                // Días que deberían tener asistencia pero no la tienen
                var diasAusencias = diasLaborales
                    .Where(d => !diasConAsistencia.Contains(d) &&
                               !permisosAprobados.Contains(d) &&
                               !incapacidades.Any(inc => d >= inc.fecha_inicio.Date && d <= inc.fecha_fin.Date))
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Días de ausencias injustificadas: {diasAusencias.Count}");

                // Calcular monto descontado basado en salario diario
                decimal montoDescontado = 0;
                if (diasAusencias.Count > 0)
                {
                    var empleado = db.Empleados.Include(e => e.Puestos).FirstOrDefault(e => e.id_empleado == idEmpleado);
                    if (empleado?.Puestos?.salario_base != null)
                    {
                        decimal salarioDiario = empleado.Puestos.salario_base / 30m;
                        montoDescontado = diasAusencias.Count * salarioDiario;
                        System.Diagnostics.Debug.WriteLine($"Salario diario: {salarioDiario}, Monto descontado: {montoDescontado}");
                    }
                }

                return (diasAusencias.Count, montoDescontado);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ConsiderarAusenciasEnCalculo: {ex.Message}");
                return (0, 0);
            }
        }

        // POST: Generar aguinaldo para todos los empleados - MEJORADO
        [HttpPost]
        public ActionResult GenerarParaTodosLosEmpleados(int anio)
        {
            try
            {
                var empleadosActivos = db.Empleados
                    .Include(e => e.Puestos)
                    .Where(e => e.estado == "Activo")
                    .ToList();

                var resultados = new List<object>();
                int exitosos = 0;
                int errores = 0;

                System.Diagnostics.Debug.WriteLine($"=== CÁLCULO MASIVO AGUINALDO {anio} ===");
                System.Diagnostics.Debug.WriteLine($"Empleados activos: {empleadosActivos.Count}");

                foreach (var empleado in empleadosActivos)
                {
                    try
                    {
                        // Verificar si ya existe aguinaldo para este empleado y año
                        var aguinaldoExistente = db.Aguinaldo
                            .FirstOrDefault(a => a.id_empleado == empleado.id_empleado && a.anio == anio);

                        if (aguinaldoExistente == null)
                        {
                            // Calcular período de aguinaldo
                            DateTime fechaInicio = new DateTime(anio - 1, 12, 1);
                            DateTime fechaFin = new DateTime(anio, 11, 30);

                            // Ajustar fecha fin si es futuro
                            if (fechaFin > DateTime.Today)
                            {
                                fechaFin = DateTime.Today;
                            }

                            // Verificar si el empleado trabajó en el período
                            if (empleado.fecha_ingreso <= fechaFin)
                            {
                                if (empleado.fecha_ingreso > fechaInicio)
                                {
                                    fechaInicio = empleado.fecha_ingreso;
                                }

                                // Obtener salarios del período
                                var salariosDelPeriodo = db.Nomina
                                    .Where(n => n.id_empleado == empleado.id_empleado &&
                                               ((n.anio == anio - 1 && n.mes == 12) ||
                                                (n.anio == anio && n.mes <= 11)))
                                    .ToList();

                                decimal sumaSalarios = 0;
                                int mesesLaborados = 0;

                                if (salariosDelPeriodo.Any())
                                {
                                    // Usar datos reales de nómina
                                    sumaSalarios = salariosDelPeriodo.Sum(s => s.salario_bruto);
                                    mesesLaborados = salariosDelPeriodo.Count();
                                }
                                else if (empleado.Puestos != null)
                                {
                                    // Usar salario base del puesto
                                    DateTime fechaCalculoInicio = empleado.fecha_ingreso > fechaInicio ? empleado.fecha_ingreso : fechaInicio;
                                    mesesLaborados = CalcularMesesCompletos(fechaCalculoInicio, fechaFin);
                                    sumaSalarios = empleado.Puestos.salario_base * mesesLaborados;
                                }

                                if (sumaSalarios > 0)
                                {
                                    // Considerar ausencias
                                    var ajustePorAusencias = ConsiderarAusenciasEnCalculo(empleado.id_empleado, fechaInicio, fechaFin);
                                    sumaSalarios -= ajustePorAusencias.montoDescontado;

                                    // Calcular aguinaldo
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
                                        estado = "Creado",
                                        meses = mesesLaborados
                                    });

                                    System.Diagnostics.Debug.WriteLine($"Aguinaldo creado - {empleado.nombre1} {empleado.apellido1}: {montoAguinaldo:C}");
                                }
                                else
                                {
                                    errores++;
                                    resultados.Add(new
                                    {
                                        empleado = empleado.nombre1 + " " + empleado.apellido1,
                                        cedula = empleado.cedula,
                                        monto = "Error",
                                        estado = "Sin datos salariales",
                                        meses = 0
                                    });
                                }
                            }
                            else
                            {
                                resultados.Add(new
                                {
                                    empleado = empleado.nombre1 + " " + empleado.apellido1,
                                    cedula = empleado.cedula,
                                    monto = "N/A",
                                    estado = "No trabajó en el período",
                                    meses = 0
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
                                estado = "Ya existía",
                                meses = aguinaldoExistente.meses_laborados
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        errores++;
                        System.Diagnostics.Debug.WriteLine($"Error con empleado {empleado.nombre1} {empleado.apellido1}: {ex.Message}");
                        resultados.Add(new
                        {
                            empleado = empleado.nombre1 + " " + empleado.apellido1,
                            cedula = empleado.cedula,
                            monto = "Error",
                            estado = "Error: " + ex.Message,
                            meses = 0
                        });
                    }
                }

                // Guardar todos los cambios
                db.SaveChanges();

                System.Diagnostics.Debug.WriteLine($"Cálculo masivo completado - Exitosos: {exitosos}, Errores: {errores}");

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
                System.Diagnostics.Debug.WriteLine($"Error general en GenerarParaTodosLosEmpleados: {ex.Message}");
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

        // MÉTODO ADICIONAL: Limpiar y recalcular aguinaldos
        [HttpPost]
        public ActionResult LimpiarYRecalcular(int anio)
        {
            try
            {
                // Eliminar todos los aguinaldos del año especificado
                var aguinaldosExistentes = db.Aguinaldo.Where(a => a.anio == anio);
                db.Aguinaldo.RemoveRange(aguinaldosExistentes);
                db.SaveChanges();

                // Recalcular para todos los empleados
                return GenerarParaTodosLosEmpleados(anio);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al limpiar y recalcular: " + ex.Message });
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