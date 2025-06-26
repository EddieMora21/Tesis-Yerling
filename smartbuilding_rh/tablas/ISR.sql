CREATE TABLE ISR (
  id_isr INT IDENTITY(1,1) PRIMARY KEY,
  anio INT NOT NULL,
  limite_inferior DECIMAL(12,2) NOT NULL,
  limite_superior DECIMAL(12,2) NOT NULL,
  porcentaje DECIMAL(5,2) NOT NULL,
  exceso DECIMAL(12,2) NOT NULL,
  credito_hijo DECIMAL(10,2) DEFAULT 1720.00,
  credito_conyuge DECIMAL(10,2) DEFAULT 2600.00,
  descripcion TEXT,
  fecha_creacion DATETIME DEFAULT GETDATE(),
  fecha_actualizacion DATETIME DEFAULT GETDATE()
)