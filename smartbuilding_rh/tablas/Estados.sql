CREATE TABLE [dbo].[Estados]
(
    [id_estado] INT IDENTITY(101,1) NOT NULL PRIMARY KEY,
    [nombre] VARCHAR(20) NOT NULL,
    [modulo] VARCHAR(20) NOT NULL
        CHECK ([modulo] IN ('Asistencia', 'Vacaciones', 'HorasExtras', 'Incapacidades', 'Permisos'))
)
