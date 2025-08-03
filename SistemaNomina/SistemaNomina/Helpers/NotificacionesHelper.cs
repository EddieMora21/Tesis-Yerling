// Helpers/NotificacionesHelper.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using SistemaNomina.Models;

namespace SistemaNomina.Helpers
{
    public static class NotificacionesHelper
    {
        /// <summary>
        /// Identifica al jefe del departamento del empleado
        /// </summary>
        public static Empleados ObtenerJefeDepartamento(int idEmpleado)
        {
            try
            {
                using (var db = new smartbuilding_rhEntities())
                {
                    // Obtener el empleado y su departamento
                    var empleado = db.Empleados
                        .Include(e => e.Puestos)
                        .Include(e => e.Puestos.Departamentos)
                        .FirstOrDefault(e => e.id_empleado == idEmpleado);

                    if (empleado == null) return null;

                    // Buscar jefe del mismo departamento
                    var jefeDepartamento = db.Empleados
                        .Include(e => e.Puestos)
                        .Include(e => e.Usuarios)
                        .Include(e => e.Usuarios.Select(u => u.Roles))
                        .Where(e => e.Puestos.id_departamento == empleado.Puestos.id_departamento)
                        .Where(e => e.Puestos.es_jefe == true && e.estado == "Activo")
                        .FirstOrDefault();

                    // Si no hay jefe marcado, buscar supervisor del departamento
                    if (jefeDepartamento == null)
                    {
                        jefeDepartamento = db.Empleados
                            .Include(e => e.Puestos)
                            .Include(e => e.Usuarios)
                            .Include(e => e.Usuarios.Select(u => u.Roles))
                            .Where(e => e.Puestos.id_departamento == empleado.Puestos.id_departamento)
                            .Where(e => e.Usuarios.Any(u => u.Roles.nombre == "Supervisor") && e.estado == "Activo")
                            .FirstOrDefault();
                    }

                    return jefeDepartamento;
                }
            }
            catch (Exception ex)
            {
                // Log del error
                System.Diagnostics.Debug.WriteLine($"Error al obtener jefe departamento: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Crea notificación automática en la tabla Notificaciones
        /// </summary>
        public static bool CrearNotificacion(int idDestinatario, int? idRemitente, string titulo, string mensaje, string tipo = "solicitud", string referenciaTipo = null, int? referenciaId = null)
        {
            try
            {
                using (var db = new smartbuilding_rhEntities())
                {
                    var notificacion = new Notificaciones
                    {
                        id_destinatario = idDestinatario,
                        id_remitente = idRemitente,
                        titulo = titulo,
                        mensaje = mensaje,
                        tipo = tipo,
                        prioridad = "normal",
                        leido = false,
                        fecha_creacion = DateTime.Now,
                        referencia_tipo = referenciaTipo,
                        referencia_id = referenciaId,
                        icono = tipo == "solicitud" ? "fas fa-calendar-alt" : "fas fa-bell",
                        color = tipo == "solicitud" ? "#ffc107" : "#17a2b8"
                    };

                    db.Notificaciones.Add(notificacion);
                    db.SaveChanges();

                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al crear notificación: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Obtiene el usuario asociado a un empleado
        /// </summary>
        public static Usuarios ObtenerUsuarioEmpleado(int idEmpleado)
        {
            try
            {
                using (var db = new smartbuilding_rhEntities())
                {
                    return db.Usuarios
                        .Include(u => u.Empleados)
                        .Include(u => u.Roles)
                        .FirstOrDefault(u => u.id_empleado == idEmpleado);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener usuario: {ex.Message}");
                return null;
            }
        }
    }
}