CREATE TABLE Liquidaciones (
  id_liquidacion INT IDENTITY(1,1) PRIMARY KEY,
  id_empleado INT NOT NULL,
  id_tipo INT NOT NULL,
  fecha_salida DATE NOT NULL,
  preaviso DECIMAL(12,2) DEFAULT 0.00,
  cesantia DECIMAL(12,2) DEFAULT 0.00,
  vacaciones_pendientes DECIMAL(12,2) DEFAULT 0.00,
  dias_vacaciones_pendientes INT DEFAULT 0,
  aguinaldo_proporcional DECIMAL(12,2) DEFAULT 0.00,
  total_liquidacion DECIMAL(12,2) NOT NULL,
  isr_liquidacion DECIMAL(12,2) DEFAULT 0.00,
  css_liquidacion DECIMAL(12,2) DEFAULT 0.00,
  ivm_liquidacion DECIMAL(12,2) DEFAULT 0.00,
  fecha_creacion DATETIME DEFAULT GETDATE(),
  fecha_actualizacion DATETIME DEFAULT GETDATE(),
  CONSTRAINT fk_liquidaciones_empleado FOREIGN KEY (id_empleado) REFERENCES Empleados (id_empleado),
  CONSTRAINT fk_liquidaciones_tipo FOREIGN KEY (id_tipo) REFERENCES TipoLiquidacion (id_tipo)
)