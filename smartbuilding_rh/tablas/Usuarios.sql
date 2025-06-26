CREATE TABLE Usuarios (
  id_usuario INT IDENTITY(1,1) PRIMARY KEY,
  id_empleado INT UNIQUE,
  usuario VARCHAR(50) NOT NULL UNIQUE,
  contrasena VARCHAR(255) NOT NULL,
  id_rol INT NOT NULL,
  primer_ingreso BIT DEFAULT 1,
  fecha_creacion DATETIME DEFAULT GETDATE(),
  fecha_actualizacion DATETIME DEFAULT GETDATE(),
  CONSTRAINT fk_usuario_empleado FOREIGN KEY (id_empleado) REFERENCES Empleados (id_empleado),
  CONSTRAINT fk_usuario_rol FOREIGN KEY (id_rol) REFERENCES Roles (id_rol)
)
