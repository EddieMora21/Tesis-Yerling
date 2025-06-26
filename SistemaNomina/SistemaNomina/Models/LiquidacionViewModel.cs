// Models/LiquidacionViewModel.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SistemaNomina.Models
{
    public class LiquidacionViewModel
    {
        public int IdEmpleado { get; set; }

        [Display(Name = "Empleado")]
        public string NombreCompleto { get; set; }

        [Display(Name = "Cédula")]
        public string Cedula { get; set; }

        [Display(Name = "Fecha de Ingreso")]
        public DateTime FechaIngreso { get; set; }

        [Display(Name = "Fecha de Salida")]
        [Required(ErrorMessage = "La fecha de salida es requerida")]
        public DateTime FechaSalida { get; set; }

        [Display(Name = "Tipo de Liquidación")]
        [Required(ErrorMessage = "El tipo de liquidación es requerido")]
        public int IdTipo { get; set; }

        [Display(Name = "Años Laborados")]
        public decimal AniosLaborados { get; set; }

        [Display(Name = "Salario Base")]
        public decimal SalarioBase { get; set; }

        [Display(Name = "Vacaciones Pendientes (Días)")]
        public int DiasVacacionesPendientes { get; set; }

        [Display(Name = "Vacaciones Pendientes (Monto)")]
        public decimal VacacionesPendientes { get; set; }

        [Display(Name = "Aguinaldo Proporcional")]
        public decimal AguinaldoProporcional { get; set; }

        [Display(Name = "Preaviso")]
        public decimal Preaviso { get; set; }

        [Display(Name = "Cesantía")]
        public decimal Cesantia { get; set; }

        [Display(Name = "Salario Bruto")]
        public decimal SalarioBruto { get; set; }

        [Display(Name = "CCSS (9.34%)")]
        public decimal CCSS { get; set; }

        [Display(Name = "IVM (2.75%)")]
        public decimal IVM { get; set; }

        [Display(Name = "ISR")]
        public decimal ISR { get; set; }

        [Display(Name = "Total Deducciones")]
        public decimal TotalDeducciones { get; set; }

        [Display(Name = "Total Liquidación")]
        public decimal TotalLiquidacion { get; set; }

        [Display(Name = "Tipo de Liquidación")]
        public string NombreTipoLiquidacion { get; set; }
    }

    public class CalculoLiquidacionViewModel
    {
        public LiquidacionViewModel Calculo { get; set; }
        public bool EsVistaPrevia { get; set; }
        public string MensajeError { get; set; }
    }
}