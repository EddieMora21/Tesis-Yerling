using System;
using System.Collections.Generic;

namespace SistemaNomina.Models
{
    public class CalculoNominaViewModel
    {
        public int IdEmpleado { get; set; }
        public string NombreCompleto { get; set; }
        public string Cedula { get; set; }
        public decimal SalarioBase { get; set; }
        public decimal HorasExtras { get; set; }
        public decimal DiasFeriados { get; set; }
        public decimal SalarioBruto { get; set; }
        public decimal CCSS { get; set; }
        public decimal IVM { get; set; }
        public decimal ISR { get; set; }
        public decimal SalarioNeto { get; set; }
        public int Mes { get; set; }
        public int Anio { get; set; }
    }

    public class VistaPreviaViewModel
    {
        public List<CalculoNominaViewModel> Resultados { get; set; }
        public int Mes { get; set; }
        public int Anio { get; set; }

        public VistaPreviaViewModel()
        {
            Resultados = new List<CalculoNominaViewModel>();
        }
    }
}