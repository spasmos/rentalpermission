# RentalPermission

`RentalPermission` anade alquileres de pago para bloques reforzados o bloqueados con candado dentro de claims configuradas.

Esta pensado para servidores administrados donde los jugadores pueden alquilar cofres, puertas, habitaciones, puestos de mercado o almacenes concretos sin recibir permisos amplios de construccion ni control permanente sobre infraestructura publica.

## Resumen

- Mod universal: instalalo en servidor y cliente.
- La autoridad del servidor decide el resultado real del alquiler y la expiracion.
- Las reglas solo se aplican dentro de claims coincidentes.
- Cada regla puede usar sus propios filtros de bloque, filtros de claim, precio, duracion y accion de expiracion.
- Los alquileres usan un item de moneda configurable, normalmente `game:gear-rusty`.
- Los jugadores confirman el alquiler en una ventana cliente antes de pagar.
- No sustituye a las claims ni a los refuerzos vanilla; anade control de pago por encima de ellos.

## Alcance Actual

Este mod esta pensado para zonas de alquiler administradas por el servidor.

Las reglas se definen en `ModConfig/rentalpermission.json`, por lo que se necesita acceso directo a los ficheros de configuracion del servidor. Los jugadores pueden alquilar, listar y renovar sus propios alquileres, pero no crean ni gestionan reglas de alquiler.

Casos de uso habituales:

- puestos de mercado con cofres alquilados,
- habitaciones de posada o apartamentos con puertas alquiladas,
- casas de clan o almacenes publicos,
- zonas de almacenamiento gestionadas por una ciudad,
- claims controladas donde los jugadores pueden reservar bloques concretos pero no toda la zona.

## Configuracion

El servidor crea el fichero real de configuracion en:

```text
ModConfig/rentalpermission.json
```

Las plantillas comentadas se incluyen como referencia:

```text
rentalpermission.template.jsonc
rentalpermission.template.es.jsonc
```

Usa las plantillas como documentacion, pero aplica los cambios reales en `ModConfig/rentalpermission.json`.

Flujo recomendado:

1. Crea una claim con una descripcion clara.
2. Anade una regla de alquiler en `Rentals`.
3. Limita la regla con `AllowedClaimDescriptions` siempre que sea posible.
4. Configura los prefijos o codigos exactos de bloque que se pueden alquilar.
5. Define precio, duracion y accion de expiracion.
6. Concede el privilegio de alquiler configurado, normalmente `rentblocks`.
7. Prueba con un jugador no admin.
8. Usa `/rentalpermission here` cuando una regla de alquiler no se aplique como esperas.

Para configuraciones estables, es preferible filtrar claims por descripcion:

- `AllowedClaimDescriptions` es lo recomendado para ciudades, mercados y habitaciones.
- `AllowedClaimIds` usa el id visible de `/land list` y puede cambiar si se borran o recrean claims.

Las duraciones se definen por regla:

```jsonc
"RentDurationUnit": "days",
"RentDuration": 7,
"MinRentDuration": 7,
"RentDurationStep": 0
```

Unidades soportadas:

```text
hours
days
months
years
```

`BasePrice` es el precio de la duracion completa configurada en `RentDuration`. Las duraciones seleccionables mas cortas se calculan proporcionalmente.

## Comandos

Comando principal:

```text
/rentalpermission
```

Alias corto:

```text
/rentperm
```

Comando de jugador:

```text
/rentalpermission mine
```

Los comandos de administracion requieren `controlserver`:

```text
/rentalpermission reload
/rentalpermission list
/rentalpermission cancel <rental id>
/rentalpermission process
/rentalpermission here
```

`/rentalpermission here` es el comando de diagnostico de configuracion mas util. Muestra las claims en la posicion actual del admin y que reglas de RentalPermission coinciden.

## Datos Persistentes

La configuracion vive en `ModConfig`, pero los datos persistentes del mod no.

```text
ModData/<world-uid>/rentalpermission/rentalpermission.state.json
```

Ese fichero guarda alquileres activos y procesados. Haz copia de seguridad antes de editarlo manualmente.

## Notas de Uso

- Concede el privilegio de alquiler solo a roles que deban poder alquilar bloques configurados.
- Mantener las zonas de alquiler acotadas y explicitas hace que la infraestructura publica sea mas facil de controlar.
- Empieza probando las expiraciones destructivas con `WarnOnly`.
- Prueba `RemoveReinforcement` y `UnlockAndRemoveReinforcement` en un mundo de staging antes de usarlas en produccion.
- Retirar refuerzos puede dejar el contenido del bloque accesible para otros jugadores.
- Activa `MarketResetEnabled` solo en zonas CANMarket muy acotadas. El puesto coincidente se resuelve cuando se crea el alquiler, y el scheduler usa despues la posicion y el codigo de bloque guardados cuando el alquiler expira.
- Usa `/rentalpermission here` dentro de la claim objetivo cuando un jugador no recibe el prompt de alquiler.

## Acciones de Expiracion

Valores disponibles para `OnExpired`:

- `WarnOnly`
- `UnlockOnly`
- `RemoveReinforcement`
- `UnlockAndRemoveReinforcement`

## Compatibilidad

- Requiere cliente, porque la confirmacion de alquiler usa una interfaz cliente.
- Disenado para convivir con claims y refuerzos vanilla.
- El control de alquiler se engancha al flujo vanilla de refuerzos para que las acciones delegadas desde otros mods tambien puedan cobrarse.
- Separado de `claimactivitypermissions` de forma intencionada.

## Changelog 1.1.0

En curso.

- Refactorizado el sistema servidor en servicios centrados en comandos, prompts de alquiler, pagos, persistencia, expiraciones, matching de claims, registro de privilegios y flujo de interaccion.
- Refactorizado el manejo cliente de prompts en componentes mas pequenos de red y dialogo.
- Mejorada la documentacion del proyecto con un README orientado al uso real.
- Eliminado el script antiguo de empaquetado de release en favor de los artefactos normales de compilacion.
- Actualizados los metadatos del mod y la version del ensamblado a `1.1.0`.
- Movidos los registros persistentes de alquiler desde `ModConfig/rentalpermission.data.json` a `ModData/<world-uid>/rentalpermission/rentalpermission.state.json`. No se migran automaticamente: si el admin quiere conservar alquileres existentes, debe copiar manualmente el contenido del fichero antiguo al nuevo fichero de estado y puede borrar el antiguo despues.
- Anadido reinicio opcional de puestos CANMarket al expirar alquileres mediante `MarketResetEnabled`, `MarketStallBlockCodePrefixes`, `MarketStallBlockCodes`, `MarketStallSearchRadiusBlocks` y `MarketStallRequireUniqueMatch`.

## Changelog 1.0.0

- Primera version publica.
- Anadidos controles de alquiler para reforzar y bloquear con candado bloques configurados.
- Anadidos moneda configurable, filtros de claim, filtros de bloque, privilegio delegado de alquiler y requisito opcional de acceso vanilla `Use`.
- Anadidos registros persistentes de alquiler.
- Anadidos listado de alquileres del jugador, renovacion presencial y descripciones obligatorias escritas por el jugador.
- Anadidos comandos admin para recargar configuracion, listar alquileres, inspeccionar claims, cancelar registros y procesar expiraciones.
- Anadida UI cliente de confirmacion de alquiler con mensajes localizados y nombres traducidos de moneda cuando estan disponibles.
- Anadidos procesamiento automatico de expiraciones, limpieza de registros procesados y acciones de expiracion.
- Anadida configuracion de duracion con `RentDurationUnit`, `RentDuration`, `MinRentDuration`, `RentDurationStep`, `DaysPerMonth` y `MonthsPerYear`.
- Anadidos diagnosticos opcionales mediante `LogIgnoredInteractions` y `/rentalpermission here`.
