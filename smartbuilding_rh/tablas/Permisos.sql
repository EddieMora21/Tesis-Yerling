CREATE TABLE Permisos (
  id_permiso INT IDENTITY(1,1) PRIMARY KEY,
  id_empleado INT NOT NULL,
  fecha DATE NOT NULL,
  horas DECIMAL(4,2) NULL,
  id_tipo_permiso INT NOT NULL,
  motivo TEXT NULL,
  id_estado INT NOT NULL,
  aprobado_por INT NULL,
  fecha_creacion DATETIME DEFAULT GETDATE(),
  fecha_actualizacion DATETIME DEFAULT GETDATE(),
  CONSTRAINT fk_permisos_empleado FOREIGN KEY (id_empleado) REFERENCES Empleados (id_empleado),
  CONSTRAINT fk_permisos_aprobador FOREIGN KEY (aprobado_por) REFERENCES Usuarios (id_usuario),
  CONSTRAINT fk_permisos_tipo FOREIGN KEY (id_tipo_permiso) REFERENCES TiposPermiso (id_tipo_permiso),
  CONSTRAINT fk_permisos_estado FOREIGN KEY (id_estado) REFERENCES Estados (id_estado)
)
