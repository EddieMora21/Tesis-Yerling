CREATE TABLE TipoLiquidacion (
  id_tipo INT IDENTITY(1,1) PRIMARY KEY,
  nombre VARCHAR(20) NOT NULL CHECK (nombre IN ('Con responsabilidad', 'Sin responsabilidad', 'Renuncia')),
  descripcion TEXT NULL,
  fecha_creacion DATETIME DEFAULT GETDATE(),
  fecha_actualizacion DATETIME DEFAULT GETDATE()
)
