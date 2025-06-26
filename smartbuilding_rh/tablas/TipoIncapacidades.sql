CREATE TABLE TipoIncapacidades (
  id_tipo INT IDENTITY(1,1) PRIMARY KEY,
  nombre VARCHAR(50) NOT NULL,
  dias_maximos INT NOT NULL,
  pago_planilla BIT NOT NULL,
  descripcion TEXT,
  fecha_creacion DATETIME DEFAULT GETDATE(),
  fecha_actualizacion DATETIME DEFAULT GETDATE()
)
