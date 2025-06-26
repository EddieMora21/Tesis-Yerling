CREATE TABLE Feriados (
  id_feriado INT IDENTITY(1,1) PRIMARY KEY,
  nombre VARCHAR(100) NOT NULL,
  fecha DATE NOT NULL,
  pago_obligatorio BIT DEFAULT 1,
  recargo DECIMAL(5,2) DEFAULT 100.00,
  descripcion TEXT,
  fecha_creacion DATETIME DEFAULT GETDATE(),
  fecha_actualizacion DATETIME DEFAULT GETDATE()
)
