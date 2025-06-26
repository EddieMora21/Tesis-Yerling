CREATE TABLE Bitacora (
  id_log INT IDENTITY(1,1) PRIMARY KEY,
  id_usuario INT NULL,
  accion VARCHAR(100) NOT NULL,
  detalle TEXT NULL,
  fecha_hora DATETIME DEFAULT GETDATE(),
  CONSTRAINT fk_bitacora_usuario FOREIGN KEY (id_usuario) REFERENCES Usuarios (id_usuario)
  )