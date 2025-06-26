CREATE TABLE Incapacidades (
  id_incapacidad INT IDENTITY(1,1) PRIMARY KEY,
  id_empleado INT NOT NULL,
  fecha_inicio DATE NOT NULL,
  fecha_fin DATE NOT NULL,
  numero_boleta VARCHAR(20) NOT NULL,
  id_tipo_incapacidad INT NULL,
  descripcion TEXT NULL,
  id_estado INT NOT NULL,
  dias_incapacidad AS (DATEDIFF(day, fecha_inicio, fecha_fin)),
  fecha_registro DATETIME DEFAULT GETDATE(),
  CONSTRAINT fk_incapacidades_empleado FOREIGN KEY (id_empleado) REFERENCES Empleados (id_empleado),
  CONSTRAINT fk_incapacidades_estado FOREIGN KEY (id_estado) REFERENCES Estados (id_estado),
  CONSTRAINT fk_incapacidad_tipo FOREIGN KEY (id_tipo_incapacidad) REFERENCES TipoIncapacidades (id_tipo)
)
