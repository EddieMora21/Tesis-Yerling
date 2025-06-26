CREATE TABLE [dbo].[EstadoCivil]
(
    [id_estado_civil] INT IDENTITY(201,1) NOT NULL PRIMARY KEY,
    [nombre] VARCHAR(20) NOT NULL UNIQUE
)