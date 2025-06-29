﻿CREATE TABLE Nomina (
  id_nomina INT IDENTITY(1,1) PRIMARY KEY,
  id_empleado INT NOT NULL,
  id_isr INT NOT NULL,
  mes INT NOT NULL,
  anio INT NOT NULL,
  salario_bruto DECIMAL(12,2) NOT NULL,
  horas_extras DECIMAL(12,2) DEFAULT 0.00,
  salario_dias_feriados DECIMAL(12,2) DEFAULT 0.00,
  ccss DECIMAL(12,2) DEFAULT 0.00,
  ivm DECIMAL(12,2) DEFAULT 0.00,
  isr DECIMAL(12,2) DEFAULT 0.00,
  credito_hijos DECIMAL(12,2) DEFAULT 0.00,
  credito_conyuge DECIMAL(12,2) DEFAULT 0.00,
  otros_descuentos DECIMAL(12,2) DEFAULT 0.00,
  base_imponible AS (salario_bruto - ccss - ivm - credito_hijos - credito_conyuge),
  salario_neto DECIMAL(12,2) NOT NULL,
  fecha_creacion DATETIME DEFAULT GETDATE(),
  fecha_actualizacion DATETIME DEFAULT GETDATE(),
  CONSTRAINT fk_nomina_empleado FOREIGN KEY (id_empleado) REFERENCES Empleados (id_empleado),
  CONSTRAINT fk_nomina_isr FOREIGN KEY (id_isr) REFERENCES ISR (id_isr),
  CONSTRAINT uk_nomina_empleado_mes_anio UNIQUE (id_empleado, mes, anio)
)
