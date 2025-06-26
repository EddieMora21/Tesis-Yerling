CREATE TABLE Asistencia (
  id_asistencia INT IDENTITY(1,1) PRIMARY KEY,
  id_empleado INT NOT NULL,
  fecha DATE NOT NULL,
  hora_entrada TIME NULL,
  hora_salida TIME NULL,
  es_feriado BIT DEFAULT 0,
  id_feriado INT NULL,
  fecha_registro DATETIME DEFAULT GETDATE(),
  CONSTRAINT fk_asistencia_feriado FOREIGN KEY (id_feriado) REFERENCES Feriados(id_feriado),
  CONSTRAINT fk_asistencia_empleado FOREIGN KEY (id_empleado) REFERENCES Empleados (id_empleado),
  CONSTRAINT uk_empleado_fecha UNIQUE (id_empleado, fecha)
)
