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
    [RoleAuthorize("Admin", "RRHH", "IT")]
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

        // GET: Aguinaldo/Create - COMPLETAMENTE CORREGIDO
        public ActionResult Create()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== AGUINALDO CREATE GET ===");

                // Cargar empleados activos con nombres completos
                var empleados = db.Empleados
                    .Where(e => e.estado == "Activo")
                    .OrderBy(e => e.apellido1)
                    .ThenBy(e => e.nombre1)
                    .ToList()
                    .Select(e => new SelectListItem
                    {
                        Value = e.id_empleado.ToString(),
                        Text = $"{e.cedula} - {e.nombre1} {e.apellido1} {e.apellido2}".Trim()
                    })
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Empleados encontrados: {empleados.Count}");

                // Agregar opción por defecto
                empleados.Insert(0, new SelectListItem
                {
                    Value = "",
                    Text = "Seleccione un empleado...",
                    Selected = true
                });

                ViewBag.id_empleado = empleados;
                ViewBag.AnioActual = DateTime.Now.Year;

                // Crear modelo vacío con valores por defecto
                var modelo = new Aguinaldo
                {
                    anio = DateTime.Now.Year,
                    fecha_creacion = DateTime.Now
                };

                System.Diagnostics.Debug.WriteLine($"Total opciones en ViewBag: {empleados.Count}");

                return View(modelo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en Create GET: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

                ViewBag.id_empleado = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "Error al cargar empleados", Selected = true }
                };
                ViewBag.AnioActual = DateTime.Now.Year;

                return View(new Aguinaldo { anio = DateTime.Now.Year });
            }
        }

        // POST: Aguinaldo/Create - COMPLETAMENTE CORREGIDO CON VALIDACIONES
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "id_aguinaldo,id_empleado,monto_total,meses_laborados,anio")] Aguinaldo aguinaldo)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== AGUINALDO CREATE POST ===");
                System.Diagnostics.Debug.WriteLine($"ID Empleado: {aguinaldo.id_empleado}");
                System.Diagnostics.Debug.WriteLine($"Monto: {aguinaldo.monto_total}");
                System.Diagnostics.Debug.WriteLine($"Meses: {aguinaldo.meses_laborados}");
                System.Diagnostics.Debug.WriteLine($"Año: {aguinaldo.anio}");

                if (ModelState.IsValid)
                {
                    // 🔧 VALIDACIÓN DE MONTO NO NEGATIVO
                    if (aguinaldo.monto_total < 0)
                    {
                        ModelState.AddModelError("monto_total", "El monto del aguinaldo no puede ser negativo.");
                        RecargarListaEmpleados(aguinaldo.id_empleado);
                        return View(aguinaldo);
                    }

                    // 🔧 VALIDACIÓN DE MESES LABORADOS
                    if (aguinaldo.meses_laborados < 0 || aguinaldo.meses_laborados > 12)
                    {
                        ModelState.AddModelError("meses_laborados", "Los meses laborados deben estar entre 0 y 12.");
                        RecargarListaEmpleados(aguinaldo.id_empleado);
                        return View(aguinaldo);
                    }

                    // 🔧 VALIDACIÓN DE AÑO
                    if (aguinaldo.anio < 2020 || aguinaldo.anio > DateTime.Now.Year + 2)
                    {
                        ModelState.AddModelError("anio", "El año debe estar entre 2020 y " + (DateTime.Now.Year + 2));
                        RecargarListaEmpleados(aguinaldo.id_empleado);
                        return View(aguinaldo);
                    }

                    // Verificar que no exista ya un aguinaldo para este empleado y año
                    var aguinaldoExistente = db.Aguinaldo
                        .FirstOrDefault(a => a.id_empleado == aguinaldo.id_empleado && a.anio == aguinaldo.anio);

                    if (aguinaldoExistente != null)
                    {
                        System.Diagnostics.Debug.WriteLine("Ya existe aguinaldo para este empleado y año");
                        ModelState.AddModelError("", $"Ya existe un aguinaldo registrado para este empleado en el año {aguinaldo.anio}");
                    }
                    else
                    {
                        // Establecer fechas
                        aguinaldo.fecha_creacion = DateTime.Now;
                        aguinaldo.fecha_actualizacion = null;

                        // Guardar en la base de datos
                        db.Aguinaldo.Add(aguinaldo);
                        db.SaveChanges();

                        // 📋 LOG AUTOMÁTICO - Aguinaldo creado
                        var currentUserId = (int?)Session["UserId"];
                        if (currentUserId != null)
                        {
                            BitacoraHelper.RegistrarAccion("CREAR_AGUINALDO",
                                $"Creado aguinaldo: Empleado ID {aguinaldo.id_empleado}, Año {aguinaldo.anio}, Monto {aguinaldo.monto_total:C}",
                                currentUserId.Value);
                        }

                        System.Diagnostics.Debug.WriteLine("Aguinaldo guardado exitosamente");
                        TempData["Success"] = "Aguinaldo registrado exitosamente";

                        return RedirectToAction("Index");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ModelState no es válido:");
                    foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
                    {
                        System.Diagnostics.Debug.WriteLine($"Error: {error.ErrorMessage}");
                    }
                }

                // Si llegamos aquí, hay errores - recargar la lista de empleados
                RecargarListaEmpleados(aguinaldo.id_empleado);
                return View(aguinaldo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en Create POST: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

                ModelState.AddModelError("", "Error interno del servidor: " + ex.Message);
                RecargarListaEmpleados(aguinaldo.id_empleado);
                return View(aguinaldo);
            }
        }

        // MÉTODO AUXILIAR: Recargar lista de empleados
        private void RecargarListaEmpleados(int? empleadoSeleccionado = null)
        {
            try
            {
                var empleados = db.Empleados
                    .Where(e => e.estado == "Activo")
                    .OrderBy(e => e.apellido1)
                    .ThenBy(e => e.nombre1)
                    .ToList()
                    .Select(e => new SelectListItem
                    {
                        Value = e.id_empleado.ToString(),
                        Text = $"{e.cedula} - {e.nombre1} {e.apellido1} {e.apellido2}".Trim(),
                        Selected = empleadoSeleccionado.HasValue && e.id_empleado == empleadoSeleccionado.Value
                    })
                    .ToList();

                empleados.Insert(0, new SelectListItem
                {
                    Value = "",
                    Text = "Seleccione un empleado...",
                    Selected = !empleadoSeleccionado.HasValue
                });

                ViewBag.id_empleado = empleados;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al recargar empleados: {ex.Message}");
                ViewBag.id_empleado = new List<SelectListItem>
                {
                    new SelectListItem { Value = "", Text = "Error al cargar empleados", Selected = true }
                };
            }
        }

        // MÉTODO PARA OBTENER INFORMACIÓN DEL EMPLEADO (AJAX) - MEJORADO
        [HttpGet]
        public ActionResult GetEmpleadoInfo(int id)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== GetEmpleadoInfo ===");
                System.Diagnostics.Debug.WriteLine($"ID recibido: {id}");

                var empleado = db.Empleados
                    .Include(e => e.Puestos)
                    .Include(e => e.Puestos.Departamentos)
                    .FirstOrDefault(e => e.id_empleado == id);

                if (empleado == null)
                {
                    System.Diagnostics.Debug.WriteLine("Empleado no encontrado");
                    return Json(new { success = false, message = "Empleado no encontrado" }, JsonRequestBehavior.AllowGet);
                }

                System.Diagnostics.Debug.WriteLine($"Empleado encontrado: {empleado.nombre1} {empleado.apellido1}");
                System.Diagnostics.Debug.WriteLine($"Puesto: {empleado.Puestos?.nombre_puesto}");
                System.Diagnostics.Debug.WriteLine($"Departamento: {empleado.Puestos?.Departamentos?.nombre}");

                var resultado = new
                {
                    success = true,
                    fechaIngreso = empleado.fecha_ingreso.ToString("dd/MM/yyyy"),
                    puesto = empleado.Puestos?.nombre_puesto ?? "No asignado",
                    departamento = empleado.Puestos?.Departamentos?.nombre ?? "No asignado",
                    salarioBase = empleado.Puestos?.salario_base.ToString("C") ?? "No disponible",
                    nombreCompleto = $"{empleado.nombre1} {empleado.apellido1} {empleado.apellido2}".Trim(),
                    cedula = empleado.cedula,
                    estado = empleado.estado
                };

                return Json(resultado, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en GetEmpleadoInfo: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                return Json(new { success = false, message = "Error al obtener información del empleado: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // MÉTODO PARA VERIFICAR AGUINALDO DUPLICADO - MEJORADO
        [HttpGet]
        public ActionResult VerificarDuplicado(int idEmpleado, int anio)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== VerificarDuplicado ===");
                System.Diagnostics.Debug.WriteLine($"ID Empleado: {idEmpleado}, Año: {anio}");

                var aguinaldoExistente = db.Aguinaldo
                    .Include(a => a.Empleados)
                    .FirstOrDefault(a => a.id_empleado == idEmpleado && a.anio == anio);

                if (aguinaldoExistente != null)
                {
                    System.Diagnostics.Debug.WriteLine("Aguinaldo duplicado encontrado");
                    return Json(new
                    {
                        existe = true,
                        monto = aguinaldoExistente.monto_total.ToString("C"),
                        fecha = aguinaldoExistente.fecha_creacion?.ToString("dd/MM/yyyy") ?? "No disponible",
                        empleado = $"{aguinaldoExistente.Empleados.nombre1} {aguinaldoExistente.Empleados.apellido1}"
                    }, JsonRequestBehavior.AllowGet);
                }

                System.Diagnostics.Debug.WriteLine("No se encontró aguinaldo duplicado");
                return Json(new { existe = false }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en VerificarDuplicado: {ex.Message}");
                return Json(new { existe = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Calcular aguinaldo automáticamente - COMPLETAMENTE CORREGIDO
        [HttpPost]
        public ActionResult CalcularAguinaldoAutomatico(int idEmpleado, int anio)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== CALCULAR AGUINALDO AUTOMATICO ===");
                System.Diagnostics.Debug.WriteLine($"ID Empleado: {idEmpleado}, Año: {anio}");

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

                System.Diagnostics.Debug.WriteLine($"Empleado: {empleado.nombre1} {empleado.apellido1}");
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
                        DateTime fechaCalculoInicio = empleado.fecha_ingreso > fechaInicio ? empleado.fecha_ingreso : fechaInicio;
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

                // 🔧 CORRECCIÓN: CALCULAR FACTOR DE AUSENCIAS EN LUGAR DE DESCUENTO FIJO
                var infoAusencias = CalcularFactorAusencias(idEmpleado, fechaInicio, fechaFin);

                // Aplicar factor de ausencias (máximo 50% de descuento para evitar negativos)
                decimal factorDescuento = Math.Min(infoAusencias.factorDescuento, 0.5m);
                decimal sumaSalariosAjustada = sumaSalarios * (1 - factorDescuento);

                System.Diagnostics.Debug.WriteLine($"Factor descuento por ausencias: {factorDescuento:P2}");
                System.Diagnostics.Debug.WriteLine($"Suma original: {sumaSalarios}");
                System.Diagnostics.Debug.WriteLine($"Suma ajustada: {sumaSalariosAjustada}");

                // APLICAR FÓRMULA LEGAL: (Suma de salarios devengados / 12)
                decimal montoAguinaldo = Math.Max(0, sumaSalariosAjustada / 12m); // 🔧 Asegurar que nunca sea negativo

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
                    sumaSalariosAjustada = sumaSalariosAjustada.ToString("C"),
                    montoAguinaldo = montoAguinaldo.ToString("C"),
                    diasAusencias = infoAusencias.diasAusencias,
                    factorDescuento = factorDescuento.ToString("P2"),
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

        // MÉTODO AUXILIAR: Calcular meses completos entre fechas
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

        // 🔧 MÉTODO AUXILIAR CORREGIDO: Calcular factor de ausencias en lugar de monto fijo
        private (int diasAusencias, decimal factorDescuento) CalcularFactorAusencias(int idEmpleado, DateTime fechaInicio, DateTime fechaFin)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== CALCULANDO FACTOR DE AUSENCIAS ===");
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
                    .Count();

                System.Diagnostics.Debug.WriteLine($"Días con asistencia: {diasConAsistencia}");

                // Obtener días de incapacidades (justificadas)
                var diasIncapacidades = db.Incapacidades
                    .Where(i => i.id_empleado == idEmpleado &&
                               i.fecha_inicio <= fechaFin &&
                               i.fecha_fin >= fechaInicio)
                    .ToList()
                    .Sum(i => (int)(i.fecha_fin.Date - i.fecha_inicio.Date).TotalDays + 1);

                System.Diagnostics.Debug.WriteLine($"Días de incapacidades: {diasIncapacidades}");

                // Obtener días de permisos aprobados (justificados)
                var diasPermisosAprobados = db.Permisos
                    .Where(p => p.id_empleado == idEmpleado &&
                               p.fecha >= fechaInicio &&
                               p.fecha <= fechaFin &&
                               p.Estados.nombre == "Aprobado")
                    .Count();

                System.Diagnostics.Debug.WriteLine($"Días de permisos aprobados: {diasPermisosAprobados}");

                // Calcular días laborales en el período (excluyendo fines de semana)
                int diasLaboralesTotales = 0;
                for (DateTime fecha = fechaInicio; fecha <= fechaFin; fecha = fecha.AddDays(1))
                {
                    if (fecha.DayOfWeek != DayOfWeek.Saturday && fecha.DayOfWeek != DayOfWeek.Sunday)
                    {
                        diasLaboralesTotales++;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Días laborales totales: {diasLaboralesTotales}");

                // Días justificados = asistencias + incapacidades + permisos aprobados
                int diasJustificados = diasConAsistencia + diasIncapacidades + diasPermisosAprobados;

                // Días de ausencias injustificadas
                int diasAusenciasInjustificadas = Math.Max(0, diasLaboralesTotales - diasJustificados);

                System.Diagnostics.Debug.WriteLine($"Días justificados: {diasJustificados}");
                System.Diagnostics.Debug.WriteLine($"Días de ausencias injustificadas: {diasAusenciasInjustificadas}");

                // 🔧 CALCULAR FACTOR DE DESCUENTO PROPORCIONAL
                // Si el empleado tiene más del 10% de ausencias injustificadas, aplicar descuento gradual
                decimal factorDescuento = 0;
                if (diasLaboralesTotales > 0)
                {
                    decimal porcentajeAusencias = (decimal)diasAusenciasInjustificadas / diasLaboralesTotales;

                    // Solo aplicar descuento si las ausencias superan el 10%
                    if (porcentajeAusencias > 0.10m)
                    {
                        // Descuento progresivo: por cada 1% adicional de ausencias, descontar 2%
                        factorDescuento = (porcentajeAusencias - 0.10m) * 2;
                        // Limitar el descuento máximo al 50%
                        factorDescuento = Math.Min(factorDescuento, 0.5m);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Factor de descuento: {factorDescuento:P2}");

                return (diasAusenciasInjustificadas, factorDescuento);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en CalcularFactorAusencias: {ex.Message}");
                return (0, 0);
            }
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

            RecargarListaEmpleados(aguinaldo.id_empleado);
            return View(aguinaldo);
        }

        // POST: Aguinaldo/Edit/5 - CON VALIDACIONES
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "id_aguinaldo,id_empleado,monto_total,meses_laborados,anio,fecha_creacion")] Aguinaldo aguinaldo)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // 🔧 VALIDACIONES IGUAL QUE EN CREATE
                    if (aguinaldo.monto_total < 0)
                    {
                        ModelState.AddModelError("monto_total", "El monto del aguinaldo no puede ser negativo.");
                        RecargarListaEmpleados(aguinaldo.id_empleado);
                        return View(aguinaldo);
                    }

                    if (aguinaldo.meses_laborados < 0 || aguinaldo.meses_laborados > 12)
                    {
                        ModelState.AddModelError("meses_laborados", "Los meses laborados deben estar entre 0 y 12.");
                        RecargarListaEmpleados(aguinaldo.id_empleado);
                        return View(aguinaldo);
                    }

                    if (aguinaldo.anio < 2020 || aguinaldo.anio > DateTime.Now.Year + 2)
                    {
                        ModelState.AddModelError("anio", "El año debe estar entre 2020 y " + (DateTime.Now.Year + 2));
                        RecargarListaEmpleados(aguinaldo.id_empleado);
                        return View(aguinaldo);
                    }

                    // Verificar duplicados (excluyendo el actual)
                    var aguinaldoExistente = db.Aguinaldo
                        .FirstOrDefault(a => a.id_empleado == aguinaldo.id_empleado &&
                                           a.anio == aguinaldo.anio &&
                                           a.id_aguinaldo != aguinaldo.id_aguinaldo);

                    if (aguinaldoExistente != null)
                    {
                        ModelState.AddModelError("", $"Ya existe otro aguinaldo registrado para este empleado en el año {aguinaldo.anio}");
                        RecargarListaEmpleados(aguinaldo.id_empleado);
                        return View(aguinaldo);
                    }

                    aguinaldo.fecha_actualizacion = DateTime.Now;
                    db.Entry(aguinaldo).State = EntityState.Modified;
                    db.SaveChanges();

                    // 📋 LOG AUTOMÁTICO - Aguinaldo editado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId != null)
                    {
                        BitacoraHelper.RegistrarAccion("EDITAR_AGUINALDO",
                            $"Editado aguinaldo: Empleado ID {aguinaldo.id_empleado}, Año {aguinaldo.anio}, Monto {aguinaldo.monto_total:C} (ID: {aguinaldo.id_aguinaldo})",
                            currentUserId.Value);
                    }

                    TempData["Success"] = "Aguinaldo actualizado exitosamente";
                    return RedirectToAction("Index");
                }

                RecargarListaEmpleados(aguinaldo.id_empleado);
                return View(aguinaldo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en Edit: {ex.Message}");
                ModelState.AddModelError("", "Error al actualizar: " + ex.Message);
                RecargarListaEmpleados(aguinaldo.id_empleado);
                return View(aguinaldo);
            }
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
            try
            {
                Aguinaldo aguinaldo = db.Aguinaldo.Find(id);
                if (aguinaldo != null)
                {
                    // 📋 LOG AUTOMÁTICO - Aguinaldo eliminado
                    var currentUserId = (int?)Session["UserId"];
                    if (currentUserId != null)
                    {
                        BitacoraHelper.RegistrarAccion("ELIMINAR_AGUINALDO",
                            $"Eliminado aguinaldo: Empleado ID {aguinaldo.id_empleado}, Año {aguinaldo.anio}, Monto {aguinaldo.monto_total:C} (ID: {aguinaldo.id_aguinaldo})",
                            currentUserId.Value);
                    }

                    db.Aguinaldo.Remove(aguinaldo);
                    db.SaveChanges();
                    TempData["Success"] = "Aguinaldo eliminado exitosamente";
                }
                else
                {
                    TempData["Error"] = "No se encontró el aguinaldo a eliminar";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al eliminar aguinaldo: {ex.Message}");
                TempData["Error"] = "Error al eliminar el aguinaldo: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // POST: Generar aguinaldo para todos los empleados - CORREGIDO
        [HttpPost]
        public ActionResult GenerarParaTodosLosEmpleados(int anio)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== GENERACIÓN MASIVA AGUINALDO {anio} ===");

                var empleadosActivos = db.Empleados
                    .Include(e => e.Puestos)
                    .Where(e => e.estado == "Activo")
                    .ToList();

                var resultados = new List<object>();
                int exitosos = 0;
                int errores = 0;

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
                                    // 🔧 USAR NUEVO MÉTODO DE FACTOR DE AUSENCIAS
                                    var infoAusencias = CalcularFactorAusencias(empleado.id_empleado, fechaInicio, fechaFin);
                                    decimal factorDescuento = Math.Min(infoAusencias.factorDescuento, 0.5m);
                                    sumaSalarios = sumaSalarios * (1 - factorDescuento);

                                    // Calcular aguinaldo (asegurar que nunca sea negativo)
                                    decimal montoAguinaldo = Math.Max(0, sumaSalarios / 12m);

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
                                        meses = mesesLaborados,
                                        factorDescuento = factorDescuento.ToString("P2")
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
                                        meses = 0,
                                        factorDescuento = "N/A"
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
                                    meses = 0,
                                    factorDescuento = "N/A"
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
                                meses = aguinaldoExistente.meses_laborados,
                                factorDescuento = "N/A"
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
                            meses = 0,
                            factorDescuento = "N/A"
                        });
                    }
                }

                // Guardar todos los cambios
                db.SaveChanges();

                // 📋 LOG AUTOMÁTICO - Generación masiva
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId != null)
                {
                    BitacoraHelper.RegistrarAccion("GENERAR_AGUINALDOS_MASIVO",
                        $"Generación masiva aguinaldos {anio}: {exitosos} exitosos, {errores} errores",
                        currentUserId.Value);
                }

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

        // MÉTODO ADICIONAL: Limpiar y recalcular aguinaldos
        [HttpPost]
        public ActionResult LimpiarYRecalcular(int anio)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== LIMPIAR Y RECALCULAR {anio} ===");

                // Eliminar todos los aguinaldos del año especificado
                var aguinaldosExistentes = db.Aguinaldo.Where(a => a.anio == anio);
                int eliminados = aguinaldosExistentes.Count();

                // 📋 LOG AUTOMÁTICO - Limpieza antes de eliminar
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId != null)
                {
                    BitacoraHelper.RegistrarAccion("LIMPIAR_AGUINALDOS",
                        $"Limpieza aguinaldos {anio}: {eliminados} registros eliminados",
                        currentUserId.Value);
                }

                db.Aguinaldo.RemoveRange(aguinaldosExistentes);
                db.SaveChanges();

                System.Diagnostics.Debug.WriteLine($"Aguinaldos eliminados: {eliminados}");

                // Recalcular para todos los empleados
                return GenerarParaTodosLosEmpleados(anio);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en LimpiarYRecalcular: {ex.Message}");
                return Json(new { success = false, message = "Error al limpiar y recalcular: " + ex.Message });
            }
        }

        // MÉTODO ADICIONAL: Obtener estadísticas de aguinaldos
        [HttpGet]
        public ActionResult ObtenerEstadisticas(int anio)
        {
            try
            {
                var aguinaldos = db.Aguinaldo
                    .Include(a => a.Empleados)
                    .Where(a => a.anio == anio)
                    .ToList();

                if (!aguinaldos.Any())
                {
                    return Json(new
                    {
                        success = true,
                        totalAguinaldos = 0,
                        montoTotal = 0,
                        promedioMonto = 0,
                        empleadosConAguinaldo = 0,
                        empleadosSinAguinaldo = 0
                    }, JsonRequestBehavior.AllowGet);
                }

                var totalEmpleadosActivos = db.Empleados.Count(e => e.estado == "Activo");
                var montoTotal = aguinaldos.Sum(a => a.monto_total);
                var promedioMonto = aguinaldos.Average(a => a.monto_total);

                return Json(new
                {
                    success = true,
                    totalAguinaldos = aguinaldos.Count,
                    montoTotal = montoTotal.ToString("C"),
                    promedioMonto = promedioMonto.ToString("C"),
                    empleadosConAguinaldo = aguinaldos.Count,
                    empleadosSinAguinaldo = totalEmpleadosActivos - aguinaldos.Count,
                    detallesPorMes = aguinaldos
                        .GroupBy(a => a.meses_laborados)
                        .Select(g => new
                        {
                            meses = g.Key,
                            cantidad = g.Count(),
                            monto = g.Sum(a => a.monto_total).ToString("C")
                        })
                        .OrderBy(x => x.meses)
                        .ToList()
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerEstadisticas: {ex.Message}");
                return Json(new { success = false, message = "Error al obtener estadísticas: " + ex.Message }, JsonRequestBehavior.AllowGet);
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