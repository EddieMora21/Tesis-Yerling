
use [smartbuilding_rh]

DECLARE @UnicoEmpleado INT;
DECLARE @UnicoDepartamento INT;
DECLARE @UnicoHorario INT;
DECLARE @UnicoPuesto INT;
DECLARE @UnicoEstadoCivil INT;
DECLARE @UnicoRol INT;
DECLARE @UnicoEstado INT;
DECLARE @UnicoUsuario INT;
DECLARE @UnicoVacaciones INT;
DECLARE @UnicoTipoLiquidacion INT;
DECLARE @UnicoTipoIncapacidades INT;
DECLARE @UnicoTiposPermiso INT;
DECLARE @UnicoISR INT;
DECLARE @UnicoTiposHoraExtra INT;


INSERT INTO Estados (nombre, modulo) VALUES 
('Pendiente', 'Vacaciones'), ('Aprobado', 'Vacaciones'), ('Rechazado', 'Vacaciones'),
('Presente', 'Asistencia'), ('Ausente', 'Asistencia'), ('Tardanza', 'Asistencia'),
('Pendiente', 'Permisos'), ('Aprobado', 'Permisos'), ('Rechazado', 'Permisos');

SET @UnicoEstado = (select top 1 [id_estado] from [dbo].Estados);

INSERT INTO EstadoCivil (nombre) VALUES 
('Soltero'), ('Casado'), ('Divorciado'), ('Viudo');

SET @UnicoEstadoCivil = (select top 1 [id_estado_civil] from [dbo].EstadoCivil);

INSERT INTO Roles (nombre, descripcion) VALUES 
('Admin', 'Acceso total al sistema'),
('RRHH', 'Gestión de empleados y nómina'),
('Supervisor', 'Aprobación de solicitudes y consultas de equipo'),
('Empleado', 'Acceso limitado');

SET @UnicoRol = (select top 1 [id_rol] from [dbo].Roles);

INSERT INTO TiposPermiso (nombre, con_goce, descripcion) VALUES 
('Enfermedad', 1, 'Permiso con goce salarial'),
('Personal', 0, 'Permiso sin goce salarial');		

SET @UnicoTiposPermiso = (select top 1 [id_tipo_permiso] from [dbo].TiposPermiso);

INSERT INTO Departamentos (nombre)
VALUES ('IT');

SET @UnicoDepartamento = (select top 1 [id_departamento] from [dbo].Departamentos);

INSERT INTO Horarios (nombre, hora_entrada, hora_salida)
VALUES ('Turno Matutino', '08:00:00', '16:00:00');

SET @UnicoHorario = (select top 1 [id_horario] from [dbo].[Horarios]);

INSERT INTO Puestos (
  nombre_puesto,
  id_departamento,
  salario_base,
  descripcion,
  horas_jornada,
  es_jefe,
  id_horario
) VALUES (
  'Analista de Sistemas', -- nombre_puesto
  @UnicoDepartamento,                     -- id_departamento (replace with a valid department ID)
  25000.00,              -- salario_base
  'Responsable del análisis y diseño de sistemas.', -- descripcion
  8,                     -- horas_jornada
  0,                     -- es_jefe
  @UnicoHorario                   -- id_horario (or a valid horario ID)
);

SET @UnicoPuesto = (select top 1 [id_puesto] from [dbo].[Puestos]);

INSERT INTO Empleados (
  cedula, nombre1, nombre2, apellido1, apellido2, fecha_nacimiento,
  direccion, correo, telefono, id_estado_civil, cantidad_hijos,
  id_puesto, id_horario, fecha_ingreso, estado
) VALUES (
  '1234567890', 'Juan', 'Carlos', 'Pérez', 'Gómez', '1990-05-15',
  '123 Main St', 'juan.perez@email.com', '555-1234', @UnicoEstadoCivil, 2,
  @UnicoPuesto, @UnicoHorario, '2025-05-22', 'Activo'
);


SET @UnicoEmpleado = (select top 1 [id_empleado] from [dbo].[Empleados]);

INSERT INTO Usuarios (
  id_empleado,
  usuario,
  contrasena,
  id_rol
) VALUES (
  @UnicoEmpleado,     -- id_empleado (replace with a valid employee ID)
  'jperez',           -- usuario (choose a unique username)
  '123',    -- contrasena (store a hashed password in production)
  @UnicoRol                 -- id_rol (replace with a valid role ID)
);

SET @UnicoUsuario = (select top 1 [id_usuario] from [dbo].Usuarios);

INSERT INTO TipoIncapacidades (
  nombre,
  dias_maximos,
  pago_planilla,
  descripcion
) VALUES (
  'Incapacidad Temporal',  -- nombre
  30,                     -- dias_maximos
  1,                      -- pago_planilla (1 = true, 0 = false)
  'Incapacidad por enfermedad común, hasta 30 días'
);

SET @UnicoTipoIncapacidades = (select top 1 [id_tipo] from [dbo].TipoIncapacidades);


INSERT INTO ISR (
  anio,
  limite_inferior,
  limite_superior,
  porcentaje,
  exceso,
  credito_hijo,
  credito_conyuge,
  descripcion
) VALUES (
  2025,            -- anio
  10000.00,        -- limite_inferior
  20000.00,        -- limite_superior
  15.00,           -- porcentaje
  500.00,          -- exceso
  1720.00,         -- credito_hijo (default, can omit if desired)
  2600.00,         -- credito_conyuge (default, can omit if desired)
  'Example ISR record for 2025'
);

SET @UnicoISR= (select top 1 [id_isr] from [dbo].ISR);

INSERT INTO Asistencia (id_empleado, fecha, hora_entrada, hora_salida, es_feriado, id_feriado)
VALUES (@UnicoEmpleado, '2025-05-22', '08:00:00', '17:00:00', 0, NULL);

INSERT INTO Feriados (nombre, fecha, pago_obligatorio, recargo, descripcion)
VALUES ('Día de la Independencia', '2025-09-15', 1, 100.00, 'Celebración nacional de la independencia');

INSERT INTO TiposHoraExtra (
  nombre,
  recargo,
  descripcion
)
VALUES (
  'Nocturna',      -- nombre
  75.00,           -- recargo (percentage)
  'Horas extra trabajadas en horario nocturno'
);

SET @UnicoTiposHoraExtra= (select top 1 [id_tipo] from [dbo].TiposHoraExtra);

INSERT INTO HorasExtras (
  id_empleado,
  id_tipo,
  fecha,
  hora_inicio,
  hora_fin,
  horas,
  valor_hora,
  recargo,
  motivo,
  id_estado,
  aprobado_por,
  fecha_aprobacion
)
VALUES (
  @UnicoEmpleado,                -- id_empleado
  @UnicoTiposHoraExtra,                -- id_tipo
  '2025-05-22',     -- fecha
  '18:00:00',       -- hora_inicio
  '20:00:00',       -- hora_fin
  2.00,             -- horas
  15.00,            -- valor_hora
  50.00,            -- recargo (percentage)
  'Trabajo urgente',-- motivo
  @UnicoEstado,                -- id_estado
  NULL,             -- aprobado_por (NULL if not approved yet)
  NULL              -- fecha_aprobacion (NULL if not approved yet)
);

INSERT INTO Vacaciones (
  id_empleado,
  periodo,
  dias_disponibles,
  dias_disfrutados
)
VALUES (
  @UnicoEmpleado,              -- id_empleado
  '2025',         -- periodo (e.g., year or period string)
  15,             -- dias_disponibles
  0               -- dias_disfrutados (default is 0, can be omitted)
);

SET @UnicoVacaciones= (select top 1 [id_vacacion] from [dbo].Vacaciones);

INSERT INTO SolicitudesVacaciones (
  id_vacacion,
  fecha_inicio,
  fecha_fin,
  aprobado_por,
  comentario_solicitud,
  comentario_respuesta,
  id_estado
)
VALUES (
  @UnicoVacaciones,                        -- id_vacacion (replace with a valid id_vacacion)
  '2025-07-01',             -- fecha_inicio
  '2025-07-10',             -- fecha_fin
  NULL,                     -- aprobado_por (NULL if not yet approved)
  'Solicito vacaciones.',   -- comentario_solicitud
  NULL,                     -- comentario_respuesta (NULL if no response yet)
  @UnicoEstado                         -- id_estado (replace with a valid id_estado)
);

INSERT INTO Incapacidades (
  id_empleado,
  fecha_inicio,
  fecha_fin,
  numero_boleta,
  id_tipo_incapacidad,
  descripcion,
  id_estado
) VALUES (
  @UnicoEmpleado, -- id_empleado
  '2025-05-01', -- fecha_inicio
  '2025-05-10', -- fecha_fin
  'NB-12345', -- numero_boleta
  @UnicoTipoIncapacidades, -- id_tipo_incapacidad (or NULL)
  'Medical leave due to surgery', -- descripcion (or NULL)
  @UnicoEstado -- id_estado
);


INSERT INTO Aguinaldo (
  id_empleado,
  monto_total,
  meses_laborados,
  anio
) VALUES (
  @UnicoEmpleado,         -- id_empleado
  15000.00,  -- monto_total
  12,        -- meses_laborados
  2025       -- anio
);

INSERT INTO Nomina (
  id_empleado,
  id_isr,
  mes,
  anio,
  salario_bruto,
  salario_neto
) VALUES (
  @UnicoEmpleado,         -- id_empleado
  @UnicoISR,         -- id_isr
  5,         -- mes (May)
  2025,      -- anio
  2000.00,   -- salario_bruto
  1800.00    -- salario_neto
);

INSERT INTO TipoLiquidacion (
  nombre,
  descripcion
) VALUES (
  'Con responsabilidad', -- nombre
  'Liquidacion completa' -- descripcion (optional)
);

SET @UnicoTipoLiquidacion= (select top 1 [id_tipo] from [dbo].TipoLiquidacion);

INSERT INTO Liquidaciones (
  id_empleado,
  id_tipo,
  fecha_salida,
  total_liquidacion
) VALUES (
  @UnicoEmpleado,            -- id_empleado
 @UnicoTipoLiquidacion,            -- id_tipo
  '2025-05-22', -- fecha_salida
  5000.00       -- total_liquidacion
);

INSERT INTO Permisos (
  id_empleado,
  fecha,
  horas,
  id_tipo_permiso,
  motivo,
  id_estado,
  aprobado_por
) VALUES (
  @UnicoEmpleado,                -- id_empleado
  '2025-05-22',     -- fecha
  4.00,             -- horas (optional, can be NULL)
  @UnicoTiposPermiso,                -- id_tipo_permiso
  'Medical appointment', -- motivo (optional, can be NULL)
  @UnicoEstado,                -- id_estado
  NULL              -- aprobado_por (optional, can be NULL)
);

INSERT INTO Bitacora (
  id_usuario,
  accion,
  detalle
) VALUES (
  @UnicoUsuario, -- id_usuario (optional, can be NULL)
  'User login', -- accion
  'User logged in successfully' -- detalle (optional, can be NULL)
);


EXEC sp_addextendedproperty 'MS_Description', 'Catálogo reutilizable para estados en diferentes módulos', 'SCHEMA', 'dbo', 'TABLE', 'Estados';

EXEC sp_addextendedproperty 'MS_Description', 'Catálogo de estado civil para empleados', 'SCHEMA', 'dbo', 'TABLE', 'EstadoCivil';

EXEC sp_addextendedproperty 'MS_Description', 'Roles de usuarios en el sistema con sus permisos', 'SCHEMA', 'dbo', 'TABLE', 'Roles';

EXEC sp_addextendedproperty 'MS_Description', 'Tipos de permisos laborales', 'SCHEMA', 'dbo', 'TABLE', 'TiposPermiso';

EXEC sp_addextendedproperty 'MS_Description', 'Si el permiso afecta el salario', 'SCHEMA', 'dbo', 'TABLE', 'TiposPermiso', 'COLUMN', 'con_goce';

EXEC sp_addextendedproperty 'MS_Description', 'Tipos de horas extras y sus recargos', 'SCHEMA', 'dbo', 'TABLE', 'TiposHoraExtra';

EXEC sp_addextendedproperty 'MS_Description', 'Porcentaje base para este tipo de hora extra', 'SCHEMA', 'dbo', 'TABLE', 'TiposHoraExtra', 'COLUMN', 'recargo';


EXEC sp_addextendedproperty 'MS_Description', 'Registro de feriados y sus reglas de pago', 'SCHEMA', 'dbo', 'TABLE', 'Feriados';

EXEC sp_addextendedproperty 'MS_Description', 'Nombre del feriado', 'SCHEMA', 'dbo', 'TABLE', 'Feriados', 'COLUMN', 'nombre';

EXEC sp_addextendedproperty 'MS_Description', 'Fecha exacta del feriado', 'SCHEMA', 'dbo', 'TABLE', 'Feriados', 'COLUMN', 'fecha';

EXEC sp_addextendedproperty 'MS_Description', 'Porcentaje adicional al salario por trabajar (ej: 100% = doble pago)', 'SCHEMA', 'dbo', 'TABLE', 'Feriados', 'COLUMN', 'recargo';

EXEC sp_addextendedproperty 'MS_Description', 'Tabla de tramos del Impuesto sobre la Renta', 'SCHEMA', 'dbo', 'TABLE', 'ISR';

EXEC sp_addextendedproperty 'MS_Description', 'Año fiscal al que aplican estos valores', 'SCHEMA', 'dbo', 'TABLE', 'ISR', 'COLUMN', 'anio';

EXEC sp_addextendedproperty 'MS_Description', 'Salario mínimo del tramo impositivo', 'SCHEMA', 'dbo', 'TABLE', 'ISR', 'COLUMN', 'limite_inferior';

EXEC sp_addextendedproperty 'MS_Description', 'Salario máximo del tramo impositivo', 'SCHEMA', 'dbo', 'TABLE', 'ISR', 'COLUMN', 'limite_superior';

EXEC sp_addextendedproperty 'MS_Description', '% de impuesto a aplicar sobre el excedente', 'SCHEMA', 'dbo', 'TABLE', 'ISR', 'COLUMN', 'porcentaje';

EXEC sp_addextendedproperty 'MS_Description', 'Monto base sobre el que se calcula el impuesto', 'SCHEMA', 'dbo', 'TABLE', 'ISR', 'COLUMN', 'exceso';

EXEC sp_addextendedproperty 'MS_Description', 'Reducción mensual por hijo', 'SCHEMA', 'dbo', 'TABLE', 'ISR', 'COLUMN', 'credito_hijo';

EXEC sp_addextendedproperty 'MS_Description', 'Reducción mensual por cónyuge', 'SCHEMA', 'dbo', 'TABLE', 'ISR', 'COLUMN', 'credito_conyuge'

EXEC sp_addextendedproperty 'MS_Description', 'Detalles legales o cambios fiscales', 'SCHEMA', 'dbo', 'TABLE', 'ISR', 'COLUMN', 'descripcion';

EXEC sp_addextendedproperty 'MS_Description', 'Tipos de incapacidades y sus características', 'SCHEMA', 'dbo', 'TABLE', 'TipoIncapacidades';

EXEC sp_addextendedproperty 'MS_Description', 'Departamentos de la empresa', 'SCHEMA', 'dbo', 'TABLE', 'Departamentos';

EXEC sp_addextendedproperty 'MS_Description', 'Catálogo de horarios laborales', 'SCHEMA', 'dbo', 'TABLE', 'Horarios';

EXEC sp_addextendedproperty 'MS_Description', 'Puestos laborales por departamento', 'SCHEMA', 'dbo', 'TABLE', 'Puestos';


EXEC sp_addextendedproperty 'MS_Description', 'Indica si el puesto es de jefatura', 'SCHEMA', 'dbo', 'TABLE', 'Puestos', 'COLUMN', 'es_jefe';


EXEC sp_addextendedproperty 'MS_Description', 'Información básica de los empleados', 'SCHEMA', 'dbo', 'TABLE', 'Empleados';


EXEC sp_addextendedproperty 'MS_Description', 'Almacena dirección completa', 'SCHEMA', 'dbo', 'TABLE', 'Empleados', 'COLUMN', 'direccion';


EXEC sp_addextendedproperty 'MS_Description', 'Relación con catálogo de estado civil', 'SCHEMA', 'dbo', 'TABLE', 'Empleados', 'COLUMN', 'id_estado_civil';


EXEC sp_addextendedproperty 'MS_Description', 'Relación con catálogo de horarios', 'SCHEMA', 'dbo', 'TABLE', 'Empleados', 'COLUMN', 'id_horario';


EXEC sp_addextendedproperty 'MS_Description', 'Usuarios del sistema con credenciales y roles', 'SCHEMA', 'dbo', 'TABLE', 'Usuarios';


EXEC sp_addextendedproperty 'MS_Description', 'Debe almacenarse como hash', 'SCHEMA', 'dbo', 'TABLE', 'Usuarios', 'COLUMN', 'contrasena';


EXEC sp_addextendedproperty 'MS_Description', 'Forzar cambio de contraseña en primer login', 'SCHEMA', 'dbo', 'TABLE', 'Usuarios', 'COLUMN', 'primer_ingreso';


EXEC sp_addextendedproperty 'MS_Description', 'Registro diario de asistencia de empleados', 'SCHEMA', 'dbo', 'TABLE', 'Asistencia';


EXEC sp_addextendedproperty 'MS_Description', 'Registro de horas extras trabajadas por empleados', 'SCHEMA', 'dbo', 'TABLE', 'HorasExtras';

EXEC sp_addextendedproperty 'MS_Description', 'Porcentaje adicional al valor hora', 'SCHEMA', 'dbo', 'TABLE', 'HorasExtras', 'COLUMN', 'recargo';

EXEC sp_addextendedproperty 'MS_Description', 'Relación con catálogo de estados', 'SCHEMA', 'dbo', 'TABLE', 'HorasExtras', 'COLUMN', 'id_estado';


EXEC sp_addextendedproperty 'MS_Description', 'Días de vacaciones disponibles y disfrutados por empleado', 'SCHEMA', 'dbo', 'TABLE', 'Vacaciones';

EXEC sp_addextendedproperty 'MS_Description', 'Periodo al que corresponden las vacaciones (ej: 2023-2024)', 'SCHEMA', 'dbo', 'TABLE', 'Vacaciones', 'COLUMN', 'periodo';


EXEC sp_addextendedproperty 'MS_Description', 'Registro detallado de solicitudes de vacaciones', 'SCHEMA', 'dbo', 'TABLE', 'SolicitudesVacaciones';

EXEC sp_addextendedproperty 'MS_Description', 'Justificación del empleado', 'SCHEMA', 'dbo', 'TABLE', 'SolicitudesVacaciones', 'COLUMN', 'comentario_solicitud';

EXEC sp_addextendedproperty 'MS_Description', 'Observaciones del aprobador/rechazador', 'SCHEMA', 'dbo', 'TABLE', 'SolicitudesVacaciones', 'COLUMN', 'comentario_respuesta';

EXEC sp_addextendedproperty 'MS_Description', 'Relación con catálogo Estados', 'SCHEMA', 'dbo', 'TABLE', 'SolicitudesVacaciones', 'COLUMN', 'id_estado';

EXEC sp_addextendedproperty 'MS_Description', 'Registro de incapacidades médicas de empleados', 'SCHEMA', 'dbo', 'TABLE', 'Incapacidades';


EXEC sp_addextendedproperty 'MS_Description', 'Registro de aguinaldos por empleado', 'SCHEMA', 'dbo', 'TABLE', 'Aguinaldo';

EXEC sp_addextendedproperty 'MS_Description', 'Registro mensual de nómina por empleado', 'SCHEMA', 'dbo', 'TABLE', 'Nomina';


EXEC sp_addextendedproperty 'MS_Description', 'Tipos de liquidación y sus características', 'SCHEMA', 'dbo', 'TABLE', 'TipoLiquidacion';

EXEC sp_addextendedproperty 'MS_Description', 'Liquidaciones finales de empleados al terminar contrato', 'SCHEMA', 'dbo', 'TABLE', 'Liquidaciones';

EXEC sp_addextendedproperty 'MS_Description', 'Monto calculado por días de preaviso', 'SCHEMA', 'dbo', 'TABLE', 'Liquidaciones', 'COLUMN', 'preaviso';

EXEC sp_addextendedproperty 'MS_Description', 'Monto calculado por días de cesantía', 'SCHEMA', 'dbo', 'TABLE', 'Liquidaciones', 'COLUMN', 'cesantia';

EXEC sp_addextendedproperty 'MS_Description', 'Permisos especiales solicitados por empleados', 'SCHEMA', 'dbo', 'TABLE', 'Permisos';

EXEC sp_addextendedproperty 'MS_Description', 'Registro de actividades importantes en el sistema', 'SCHEMA', 'dbo', 'TABLE', 'Bitacora';

EXEC sp_addextendedproperty 'MS_Description', '"Login", "Aprobó vacaciones", "Actualizó empleado"', 'SCHEMA', 'dbo', 'TABLE', 'Bitacora', 'COLUMN', 'accion';

EXEC sp_addextendedproperty 'MS_Description', 'JSON/Texto con datos relevantes de la acción', 'SCHEMA', 'dbo', 'TABLE', 'Bitacora', 'COLUMN', 'detalle';
