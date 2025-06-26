CREATE TABLE Aguinaldo (
  id_aguinaldo INT IDENTITY(1,1) PRIMARY KEY,
  id_empleado INT NOT NULL,
  monto_total DECIMAL(12,2) NOT NULL,
  meses_laborados INT NOT NULL,
  anio INT NOT NULL,
  fecha_creacion DATETIME DEFAULT GETDATE(),
  fecha_actualizacion DATETIME DEFAULT GETDATE(),
  CONSTRAINT fk_aguinaldo_empleado FOREIGN KEY (id_empleado) REFERENCES Empleados (id_empleado)
)
