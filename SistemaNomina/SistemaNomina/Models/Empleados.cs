//------------------------------------------------------------------------------
// <auto-generated>
//     Este código se generó a partir de una plantilla.
//
//     Los cambios manuales en este archivo pueden causar un comportamiento inesperado de la aplicación.
//     Los cambios manuales en este archivo se sobrescribirán si se regenera el código.
// </auto-generated>
//------------------------------------------------------------------------------

namespace SistemaNomina.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class Empleados
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Empleados()
        {
            this.Aguinaldo = new HashSet<Aguinaldo>();
            this.Asistencia = new HashSet<Asistencia>();
            this.HorasExtras = new HashSet<HorasExtras>();
            this.Incapacidades = new HashSet<Incapacidades>();
            this.Liquidaciones = new HashSet<Liquidaciones>();
            this.Nomina = new HashSet<Nomina>();
            this.Permisos = new HashSet<Permisos>();
            this.Usuarios = new HashSet<Usuarios>();
            this.Vacaciones = new HashSet<Vacaciones>();
        }
    
        public int id_empleado { get; set; }
        public string cedula { get; set; }
        public string nombre1 { get; set; }
        public string nombre2 { get; set; }
        public string apellido1 { get; set; }
        public string apellido2 { get; set; }
        public Nullable<System.DateTime> fecha_nacimiento { get; set; }
        public string direccion { get; set; }
        public string correo { get; set; }
        public string telefono { get; set; }
        public Nullable<int> id_estado_civil { get; set; }
        public Nullable<int> cantidad_hijos { get; set; }
        public int id_puesto { get; set; }
        public Nullable<int> id_horario { get; set; }
        public System.DateTime fecha_ingreso { get; set; }
        public string estado { get; set; }
        public Nullable<System.DateTime> fecha_creacion { get; set; }
        public Nullable<System.DateTime> fecha_actualizacion { get; set; }
    
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Aguinaldo> Aguinaldo { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Asistencia> Asistencia { get; set; }
        public virtual EstadoCivil EstadoCivil { get; set; }
        public virtual Horarios Horarios { get; set; }
        public virtual Puestos Puestos { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<HorasExtras> HorasExtras { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Incapacidades> Incapacidades { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Liquidaciones> Liquidaciones { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Nomina> Nomina { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Permisos> Permisos { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Usuarios> Usuarios { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Vacaciones> Vacaciones { get; set; }
    }
}
