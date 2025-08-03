using System;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Mvc;
using SistemaNomina.Filters;
using SistemaNomina.Helpers;
using SistemaNomina.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace SistemaNomina.Controllers
{
    [RoleAuthorize("Admin", "RRHH", "Supervisor", "Empleado")]
    public class AsistenciaController : Controller
    {
        private smartbuilding_rhEntities db = new smartbuilding_rhEntities();

        // Vista principal de asistencia
        public ActionResult Index()
        {
            var asistencia = db.Asistencia.Include(a => a.Empleados)
                                        .Include(a => a.Feriados)
                                        .OrderByDescending(a => a.fecha)
                                        .ThenByDescending(a => a.fecha_registro);
            return View(asistencia.ToList());
        }

        // 🔥 NUEVA: Exportar reporte a PDF
        [HttpPost]
        [RoleAuthorize("Admin", "RRHH", "Supervisor")]
        public ActionResult ExportarReportePDF(int? empleadoId, int? departamentoId,
                                              DateTime? fechaInicio, DateTime? fechaFin)
        {
            try
            {
                var query = db.Asistencia.Include(a => a.Empleados)
                                       .Include(a => a.Empleados.Puestos)
                                       .Include(a => a.Empleados.Puestos.Departamentos)
                                       .Include(a => a.Feriados)
                                       .AsQueryable();

                // Aplicar filtros
                if (empleadoId!= null)
                    query = query.Where(a => a.id_empleado == empleadoId.Value);

                if (departamentoId!= null)
                    query = query.Where(a => a.Empleados.Puestos.id_departamento == departamentoId.Value);

                if (fechaInicio!= null)
                    query = query.Where(a => a.fecha >= fechaInicio.Value);

                if (fechaFin!= null)
                    query = query.Where(a => a.fecha <= fechaFin.Value);

                var asistencias = query.OrderBy(a => a.fecha)
                                     .ThenBy(a => a.Empleados.apellido1)
                                     .ToList();

                // Crear documento PDF
                var document = new Document(PageSize.A4.Rotate(), 25, 25, 30, 30);
                var stream = new MemoryStream();
                var writer = PdfWriter.GetInstance(document, stream);

                document.Open();

                // ✅ Fuentes corregidas - especificando el namespace completo
                var titleFont = FontFactory.GetFont("Arial", 18, iTextSharp.text.Font.BOLD, new BaseColor(33, 29, 66));
                var subtitleFont = FontFactory.GetFont("Arial", 14, iTextSharp.text.Font.BOLD, new BaseColor(23, 136, 202));
                var headerFont = FontFactory.GetFont("Arial", 12, iTextSharp.text.Font.BOLD, BaseColor.WHITE);
                var normalFont = FontFactory.GetFont("Arial", 10, iTextSharp.text.Font.NORMAL, BaseColor.BLACK);

                var title = new Paragraph("SMART BUILDING SOLUTIONS\n", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                document.Add(title);

                var subtitle = new Paragraph("Reporte de Asistencia\n\n", subtitleFont);
                subtitle.Alignment = Element.ALIGN_CENTER;
                document.Add(subtitle);

                // Información del filtro
                var filterInfo = new Paragraph();
                filterInfo.Add(new Chunk("Período: ", headerFont));
                filterInfo.Add(new Chunk($"{(fechaInicio?.ToString("dd/MM/yyyy") ?? "Inicio")} - {(fechaFin?.ToString("dd/MM/yyyy") ?? "Fin")}\n", normalFont));
                filterInfo.Add(new Chunk("Fecha de generación: ", headerFont));
                filterInfo.Add(new Chunk($"{DateTime.Now:dd/MM/yyyy HH:mm}\n\n", normalFont));
                document.Add(filterInfo);

                // Crear tabla
                var table = new PdfPTable(8);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 12f, 20f, 15f, 12f, 12f, 10f, 12f, 12f });

                // Headers
                var headers = new string[] { "Fecha", "Empleado", "Cédula", "Entrada", "Salida", "Horas", "Estado", "Feriado" };
                foreach (var header in headers)
                {
                    var cell = new PdfPCell(new Phrase(header, headerFont));
                    cell.BackgroundColor = new BaseColor(33, 29, 66);
                    cell.HorizontalAlignment = Element.ALIGN_CENTER;
                    cell.Padding = 8;
                    cell.BorderColor = BaseColor.WHITE;
                    cell.BorderWidth = 1;
                    table.AddCell(cell);
                }

                // Datos
                foreach (var item in asistencias)
                {
                    table.AddCell(new PdfPCell(new Phrase(item.fecha.ToString("dd/MM/yyyy"), normalFont)));
                    table.AddCell(new PdfPCell(new Phrase($"{item.Empleados.nombre1} {item.Empleados.apellido1}", normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(item.Empleados.cedula, normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(item.hora_entrada?.ToString(@"hh\:mm") ?? "-", normalFont)));
                    table.AddCell(new PdfPCell(new Phrase(item.hora_salida?.ToString(@"hh\:mm") ?? "-", normalFont)));

                    var horas = item.hora_entrada!= null && item.hora_salida!= null
                               ? Math.Round((item.hora_salida.Value - item.hora_entrada.Value).TotalHours, 2).ToString()
                               : "-";
                    table.AddCell(new PdfPCell(new Phrase(horas, normalFont)));

                    var estado = item.hora_entrada!= null && item.hora_salida!= null ? "Completo"
                               : item.hora_entrada!= null ? "Sin Salida" : "Sin Entrada";
                    table.AddCell(new PdfPCell(new Phrase(estado, normalFont)));

                    table.AddCell(new PdfPCell(new Phrase(item.es_feriado == true ? "Sí" : "No", normalFont)));
                }

                document.Add(table);

                // Estadísticas al final
                var stats = new Paragraph($"\n\nResumen:\n", subtitleFont);
                stats.Add(new Chunk($"Total de registros: {asistencias.Count}\n", normalFont));
                stats.Add(new Chunk($"Asistencias completas: {asistencias.Count(x => x.hora_entrada!= null && x.hora_salida!= null)}\n", normalFont));
                stats.Add(new Chunk($"Días feriados: {asistencias.Count(x => x.es_feriado == true)}\n", normalFont));
                document.Add(stats);

                document.Close();

                var fileName = $"Reporte_Asistencia_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                return File(stream.ToArray(), "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al generar PDF: " + ex.Message });
            }
        }

        // 🔥 NUEVA: Exportar reporte a Excel
        [HttpPost]
        [RoleAuthorize("Admin", "RRHH", "Supervisor")]
        public ActionResult ExportarReporteExcel(int? empleadoId, int? departamentoId,
                                        DateTime? fechaInicio, DateTime? fechaFin)
        {
            try
            {
                var query = db.Asistencia.Include(a => a.Empleados)
                                       .Include(a => a.Empleados.Puestos)
                                       .Include(a => a.Empleados.Puestos.Departamentos)
                                       .Include(a => a.Feriados)
                                       .AsQueryable();

                // Aplicar filtros
                if (empleadoId!= null)
                    query = query.Where(a => a.id_empleado == empleadoId.Value);

                if (departamentoId!= null)
                    query = query.Where(a => a.Empleados.Puestos.id_departamento == departamentoId.Value);

                if (fechaInicio!= null)
                    query = query.Where(a => a.fecha >= fechaInicio.Value);

                if (fechaFin!= null)
                    query = query.Where(a => a.fecha <= fechaFin.Value);

                var asistencias = query.OrderBy(a => a.fecha)
                                     .ThenBy(a => a.Empleados.apellido1)
                                     .ToList();

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Reporte Asistencia");

                    // ✅ TÍTULO PRINCIPAL
                    worksheet.Cells["A1:I1"].Merge = true;
                    worksheet.Cells["A1"].Value = "SMART BUILDING SOLUTIONS";
                    worksheet.Cells["A1"].Style.Font.Size = 18;
                    worksheet.Cells["A1"].Style.Font.Bold = true;
                    worksheet.Cells["A1"].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(33, 29, 66));
                    worksheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    // ✅ SUBTÍTULO
                    worksheet.Cells["A2:I2"].Merge = true;
                    worksheet.Cells["A2"].Value = "Reporte de Asistencia";
                    worksheet.Cells["A2"].Style.Font.Size = 14;
                    worksheet.Cells["A2"].Style.Font.Bold = true;
                    worksheet.Cells["A2"].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(23, 136, 202));
                    worksheet.Cells["A2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    // ✅ INFORMACIÓN DEL PERÍODO
                    worksheet.Cells["A3:I3"].Merge = true;
                    var filtroInfo = $"Período: {(fechaInicio?.ToString("dd/MM/yyyy") ?? "Inicio")} - {(fechaFin?.ToString("dd/MM/yyyy") ?? "Fin")} | Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
                    worksheet.Cells["A3"].Value = filtroInfo;
                    worksheet.Cells["A3"].Style.Font.Size = 10;
                    worksheet.Cells["A3"].Style.Font.Italic = true;
                    worksheet.Cells["A3"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    // ✅ HEADERS (FILA 5) - Compatible con EPPlus 4.x
                    var headers = new string[] { "Fecha", "Empleado", "Cédula", "Departamento", "Entrada", "Salida", "Horas", "Estado", "Feriado" };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cells[5, i + 1];
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Font.Size = 12;
                        cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(33, 29, 66));
                        cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;

                        // ✅ Bordes compatibles con EPPlus 4.x
                        cell.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                        cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        cell.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                        cell.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                        cell.Style.Border.Top.Color.SetColor(System.Drawing.Color.White);
                        cell.Style.Border.Bottom.Color.SetColor(System.Drawing.Color.White);
                        cell.Style.Border.Left.Color.SetColor(System.Drawing.Color.White);
                        cell.Style.Border.Right.Color.SetColor(System.Drawing.Color.White);
                    }

                    // ✅ DATOS (EMPEZANDO EN FILA 6)
                    int startRow = 6;
                    for (int row = 0; row < asistencias.Count; row++)
                    {
                        var item = asistencias[row];
                        var excelRow = startRow + row;

                        // Fecha
                        worksheet.Cells[excelRow, 1].Value = item.fecha.ToString("dd/MM/yyyy");
                        worksheet.Cells[excelRow, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        // Empleado
                        worksheet.Cells[excelRow, 2].Value = $"{item.Empleados.nombre1} {item.Empleados.apellido1}";

                        // Cédula
                        worksheet.Cells[excelRow, 3].Value = item.Empleados.cedula;
                        worksheet.Cells[excelRow, 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        // Departamento
                        worksheet.Cells[excelRow, 4].Value = item.Empleados.Puestos.Departamentos.nombre;

                        // Entrada
                        worksheet.Cells[excelRow, 5].Value = item.hora_entrada?.ToString(@"hh\:mm") ?? "";
                        worksheet.Cells[excelRow, 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        // Salida
                        worksheet.Cells[excelRow, 6].Value = item.hora_salida?.ToString(@"hh\:mm") ?? "";
                        worksheet.Cells[excelRow, 6].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        // Horas trabajadas
                        if (item.hora_entrada!= null && item.hora_salida!= null)
                        {
                            var horas = Math.Round((item.hora_salida.Value - item.hora_entrada.Value).TotalHours, 2);
                            worksheet.Cells[excelRow, 7].Value = horas;
                            worksheet.Cells[excelRow, 7].Style.Numberformat.Format = "0.00";
                        }
                        worksheet.Cells[excelRow, 7].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        // Estado con colores
                        var estado = item.hora_entrada!= null && item.hora_salida!= null ? "Completo"
                                   : item.hora_entrada!= null ? "Sin Salida" : "Sin Entrada";
                        worksheet.Cells[excelRow, 8].Value = estado;
                        worksheet.Cells[excelRow, 8].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                        // Aplicar colores según estado
                        if (estado == "Completo")
                        {
                            worksheet.Cells[excelRow, 8].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(21, 87, 36)); // Verde
                        }
                        else if (estado == "Sin Salida")
                        {
                            worksheet.Cells[excelRow, 8].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(133, 100, 4)); // Amarillo oscuro
                        }
                        else
                        {
                            worksheet.Cells[excelRow, 8].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(114, 28, 36)); // Rojo
                        }

                        // Feriado
                        worksheet.Cells[excelRow, 9].Value = item.es_feriado == true ? "Sí" : "No";
                        worksheet.Cells[excelRow, 9].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                        if (item.es_feriado == true)
                        {
                            worksheet.Cells[excelRow, 9].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(12, 84, 96)); // Azul
                        }

                        // ✅ Bordes para todas las celdas de datos - Compatible con EPPlus 4.x
                        for (int col = 1; col <= 9; col++)
                        {
                            var cellBorder = worksheet.Cells[excelRow, col];
                            cellBorder.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                            cellBorder.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                            cellBorder.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                            cellBorder.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                            cellBorder.Style.Border.Top.Color.SetColor(System.Drawing.Color.Gray);
                            cellBorder.Style.Border.Bottom.Color.SetColor(System.Drawing.Color.Gray);
                            cellBorder.Style.Border.Left.Color.SetColor(System.Drawing.Color.Gray);
                            cellBorder.Style.Border.Right.Color.SetColor(System.Drawing.Color.Gray);
                        }

                        // ✅ Filas alternas con color de fondo
                        if (row % 2 == 0)
                        {
                            for (int col = 1; col <= 9; col++)
                            {
                                worksheet.Cells[excelRow, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                                worksheet.Cells[excelRow, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(248, 249, 250));
                            }
                        }
                    }

                    // ✅ ESTADÍSTICAS AL FINAL
                    int statsRow = startRow + asistencias.Count + 2;
                    worksheet.Cells[statsRow, 1].Value = "RESUMEN:";
                    worksheet.Cells[statsRow, 1].Style.Font.Bold = true;
                    worksheet.Cells[statsRow, 1].Style.Font.Size = 12;
                    worksheet.Cells[statsRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(33, 29, 66));

                    worksheet.Cells[statsRow + 1, 1].Value = $"Total de registros: {asistencias.Count}";
                    worksheet.Cells[statsRow + 2, 1].Value = $"Asistencias completas: {asistencias.Count(x => x.hora_entrada!= null && x.hora_salida!= null)}";
                    worksheet.Cells[statsRow + 3, 1].Value = $"Días feriados: {asistencias.Count(x => x.es_feriado == true)}";

                    // ✅ Auto-ajustar columnas
                    worksheet.Cells.AutoFitColumns();

                    // ✅ Ajustar ancho mínimo de columnas
                    worksheet.Column(1).Width = 12; // Fecha
                    worksheet.Column(2).Width = 25; // Empleado
                    worksheet.Column(3).Width = 15; // Cédula
                    worksheet.Column(4).Width = 20; // Departamento
                    worksheet.Column(5).Width = 10; // Entrada
                    worksheet.Column(6).Width = 10; // Salida
                    worksheet.Column(7).Width = 10; // Horas
                    worksheet.Column(8).Width = 12; // Estado
                    worksheet.Column(9).Width = 10; // Feriado

                    var fileName = $"Reporte_Asistencia_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                    return File(package.GetAsByteArray(),
                               "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                               fileName);
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al generar Excel: " + ex.Message });
            }
        }

        // 🔥 NUEVA: Vista para marcar entrada/salida
        public ActionResult Marcar()
        {
            var currentUserId = (int?)Session["UserId"];
            if (currentUserId == null)
            {
                return RedirectToAction("Login", "Usuarios");
            }

            // Buscar empleado del usuario actual
            var empleado = db.Usuarios.Include(u => u.Empleados)
                                    .Include(u => u.Empleados.Puestos)
                                    .Include(u => u.Empleados.Puestos.Departamentos)
                                    .FirstOrDefault(u => u.id_usuario == currentUserId.Value)?.Empleados;

            if (empleado == null)
            {
                TempData["Error"] = "No se encontró información del empleado asociado.";
                return RedirectToAction("Index", "Home");
            }

            // Verificar asistencia del día actual
            var hoy = DateTime.Today;
            var asistenciaHoy = db.Asistencia.FirstOrDefault(a => a.id_empleado == empleado.id_empleado
                                                              && DbFunctions.TruncateTime(a.fecha) == hoy);

            // Verificar si es feriado
            var feriado = db.Feriados.FirstOrDefault(f => DbFunctions.TruncateTime(f.fecha) == hoy);

            ViewBag.Empleado = empleado;
            ViewBag.AsistenciaHoy = asistenciaHoy;
            ViewBag.EsFeriado = feriado != null;
            ViewBag.Feriado = feriado;

            return View();
        }

        // 🔥 NUEVA: Marcar entrada
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarcarEntrada()
        {
            try
            {
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId == null)
                {
                    return Json(new { success = false, message = "Sesión expirada" });
                }

                var empleado = db.Usuarios.Include(u => u.Empleados)
                                        .FirstOrDefault(u => u.id_usuario == currentUserId.Value)?.Empleados;

                if (empleado == null)
                {
                    return Json(new { success = false, message = "Empleado no encontrado" });
                }

                var hoy = DateTime.Today;
                var asistenciaExistente = db.Asistencia.FirstOrDefault(a => a.id_empleado == empleado.id_empleado
                                                                         && DbFunctions.TruncateTime(a.fecha) == hoy);

                if (asistenciaExistente != null)
                {
                    return Json(new { success = false, message = "Ya marcó entrada hoy" });
                }

                var feriado = db.Feriados.FirstOrDefault(f => DbFunctions.TruncateTime(f.fecha) == hoy);

                var nuevaAsistencia = new Asistencia
                {
                    id_empleado = empleado.id_empleado,
                    fecha = DateTime.Now.Date,
                    hora_entrada = DateTime.Now.TimeOfDay,
                    es_feriado = feriado != null,
                    id_feriado = feriado?.id_feriado,
                    fecha_registro = DateTime.Now
                };

                db.Asistencia.Add(nuevaAsistencia);
                db.SaveChanges();

                // 📋 LOG AUTOMÁTICO
                if (System.IO.File.Exists(Server.MapPath("~/Helpers/BitacoraHelper.cs")))
                {
                    BitacoraHelper.RegistrarAccion("MARCAR_ENTRADA",
                        $"Empleado {empleado.nombre1} {empleado.apellido1} marcó entrada",
                        currentUserId.Value);
                }

                return Json(new
                {
                    success = true,
                    message = "Entrada marcada exitosamente",
                    hora = DateTime.Now.ToString("HH:mm")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al marcar entrada: " + ex.Message });
            }
        }

        // 🔥 NUEVA: Marcar salida
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MarcarSalida()
        {
            try
            {
                var currentUserId = (int?)Session["UserId"];
                if (currentUserId == null)
                {
                    return Json(new { success = false, message = "Sesión expirada" });
                }

                var empleado = db.Usuarios.Include(u => u.Empleados)
                                        .FirstOrDefault(u => u.id_usuario == currentUserId.Value)?.Empleados;

                if (empleado == null)
                {
                    return Json(new { success = false, message = "Empleado no encontrado" });
                }

                var hoy = DateTime.Today;
                var asistenciaHoy = db.Asistencia.FirstOrDefault(a => a.id_empleado == empleado.id_empleado
                                                               && DbFunctions.TruncateTime(a.fecha) == hoy);

                if (asistenciaHoy == null)
                {
                    return Json(new { success = false, message = "Debe marcar entrada primero" });
                }

                if (asistenciaHoy.hora_salida!= null)
                {
                    return Json(new { success = false, message = "Ya marcó salida hoy" });
                }

                asistenciaHoy.hora_salida = DateTime.Now.TimeOfDay;
                db.Entry(asistenciaHoy).State = EntityState.Modified;
                db.SaveChanges();

                // 📋 LOG AUTOMÁTICO
                if (System.IO.File.Exists(Server.MapPath("~/Helpers/BitacoraHelper.cs")))
                {
                    BitacoraHelper.RegistrarAccion("MARCAR_SALIDA",
                        $"Empleado {empleado.nombre1} {empleado.apellido1} marcó salida",
                        currentUserId.Value);
                }

                return Json(new
                {
                    success = true,
                    message = "Salida marcada exitosamente",
                    hora = DateTime.Now.ToString("HH:mm")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al marcar salida: " + ex.Message });
            }
        }

        // 🔥 NUEVA: Reportes de asistencia
        // 🔥 NUEVA: Reportes de asistencia
        [RoleAuthorize("Admin", "RRHH", "Supervisor")]
        public ActionResult Reportes()
        {
            // ✅ MODIFICACIÓN: Cargar empleados con cédula y nombre completo
            var empleados = db.Empleados.Where(e => e.estado == "ACTIVO")
                                       .Select(e => new {
                                           id_empleado = e.id_empleado,
                                           display = e.cedula + " - " + e.nombre1 + " " +
                                                   (e.nombre2 != null ? e.nombre2 + " " : "") +
                                                   e.apellido1 + " " +
                                                   (e.apellido2 != null ? e.apellido2 : "")
                                       })
                                       .OrderBy(e => e.display)
                                       .ToList();

            ViewBag.Empleados = new SelectList(empleados, "id_empleado", "display");
            ViewBag.Departamentos = new SelectList(db.Departamentos, "id_departamento", "nombre");
            return View();
        }

        // 🔥 NUEVA: Generar reporte filtrado
        [HttpPost]
        [RoleAuthorize("Admin", "RRHH", "Supervisor")]
        public ActionResult GenerarReporte(int? empleadoId, int? departamentoId,
                                         DateTime? fechaInicio, DateTime? fechaFin)
        {
            try
            {
                var query = db.Asistencia.Include(a => a.Empleados)
                                       .Include(a => a.Empleados.Puestos)
                                       .Include(a => a.Empleados.Puestos.Departamentos)
                                       .Include(a => a.Feriados)
                                       .AsQueryable();

                // Filtros
                if (empleadoId!= null)
                {
                    query = query.Where(a => a.id_empleado == empleadoId.Value);
                }

                if (departamentoId!= null)
                {
                    query = query.Where(a => a.Empleados.Puestos.id_departamento == departamentoId.Value);
                }

                if (fechaInicio!= null)
                {
                    query = query.Where(a => a.fecha >= fechaInicio.Value);
                }

                if (fechaFin!= null)
                {
                    query = query.Where(a => a.fecha <= fechaFin.Value);
                }

                var resultado = query.OrderBy(a => a.fecha)
                                   .ThenBy(a => a.Empleados.apellido1)
                                   .ToList();

                return PartialView("_ReporteResultado", resultado);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al generar reporte: " + ex.Message });
            }
        }

        // Métodos CRUD existentes (mantener)
        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Asistencia asistencia = db.Asistencia.Find(id);
            if (asistencia == null)
                return HttpNotFound();

            return View(asistencia);
        }

        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Create()
        {
            ViewBag.id_empleado = new SelectList(db.Empleados.Where(e => e.estado == "ACTIVO"),
                                               "id_empleado", "cedula");
            ViewBag.id_feriado = new SelectList(db.Feriados, "id_feriado", "nombre");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Create([Bind(Include = "id_empleado,fecha,hora_entrada,hora_salida,es_feriado,id_feriado")] Asistencia asistencia)
        {
            if (ModelState.IsValid)
            {
                asistencia.fecha_registro = DateTime.Now;
                db.Asistencia.Add(asistencia);
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.id_empleado = new SelectList(db.Empleados.Where(e => e.estado == "ACTIVO"),
                                               "id_empleado", "cedula", asistencia.id_empleado);
            ViewBag.id_feriado = new SelectList(db.Feriados, "id_feriado", "nombre", asistencia.id_feriado);
            return View(asistencia);
        }

        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Edit(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Asistencia asistencia = db.Asistencia.Find(id);
            if (asistencia == null)
                return HttpNotFound();

            ViewBag.id_empleado = new SelectList(db.Empleados.Where(e => e.estado == "ACTIVO"),
                                               "id_empleado", "cedula", asistencia.id_empleado);
            ViewBag.id_feriado = new SelectList(db.Feriados, "id_feriado", "nombre", asistencia.id_feriado);
            return View(asistencia);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Edit([Bind(Include = "id_asistencia,id_empleado,fecha,hora_entrada,hora_salida,es_feriado,fecha_registro,id_feriado")] Asistencia asistencia)
        {
            if (ModelState.IsValid)
            {
                db.Entry(asistencia).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }

            ViewBag.id_empleado = new SelectList(db.Empleados.Where(e => e.estado == "ACTIVO"),
                                               "id_empleado", "cedula", asistencia.id_empleado);
            ViewBag.id_feriado = new SelectList(db.Feriados, "id_feriado", "nombre", asistencia.id_feriado);
            return View(asistencia);
        }

        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult Delete(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Asistencia asistencia = db.Asistencia.Find(id);
            if (asistencia == null)
                return HttpNotFound();

            return View(asistencia);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Admin", "RRHH")]
        public ActionResult DeleteConfirmed(int id)
        {
            Asistencia asistencia = db.Asistencia.Find(id);
            db.Asistencia.Remove(asistencia);
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}