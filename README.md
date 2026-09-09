# Banreservas API Mock

Mock local de los endpoints de autenticación y posición consolidada. No usa credenciales, tokens ni datos reales.

## Ejecutar

### Visual Studio

Abra `BanreservasApiMock.sln`, establezca **BanreservasApiMock** como proyecto de inicio si Visual Studio lo solicita y presione `F5` o el botón de inicio. El perfil predeterminado es `http` y abre el servicio en el puerto 9464.

### Terminal

```powershell
dotnet run
```

Escucha en `http://localhost:9464`. Para HTTPS en `https://localhost:9465`, instala primero el certificado de desarrollo y usa el perfil correspondiente:

```powershell
dotnet dev-certs https --trust
dotnet run --launch-profile https
```

Use [BanreservasApiMock.http](BanreservasApiMock.http) o ejecute `./verify.ps1` tras `dotnet build`.

## API y operación

- Swagger UI: `http://localhost:9464/swagger`
- OpenAPI JSON: `http://localhost:9464/swagger/v1/swagger.json`
- Health check: `http://localhost:9464/health`

Swagger documenta el token Bearer ficticio. Para `/api/v1/auth`, agregue manualmente los headers `Usuario` y `Llave` indicados abajo.

## Contrato

`POST /api/v1/auth` requiere los headers `Usuario: mock-usuario` y `Llave: mock-llave`, además de `id_consumidor`. Devuelve siempre `mock-access-token`.

`POST /productos/v1/posicionconsolidada` requiere `Authorization: Bearer mock-access-token` y los campos documentados. `fechaHora` debe ser una fecha válida con formato `yyyy-MM-dd HH:mm:ss`; el ejemplo original contiene una fecha inválida y no se acepta aquí.

Los errores devuelven exclusivamente `{ "mensaje": "..." }`. `X-Mock-Delay-Ms` permite retrasar cualquier respuesta entre 0 y 30000 ms.

## Escenarios

| Identificación | Resultado |
| --- | --- |
| `40212546473` | Caso base: 6 cuentas, 1 tarjeta y 11 préstamos. |
| `00112345678` | Caso extendido con certificados y tarjetas de débito. |
| `00100000000` | Cliente sin productos. |
| `00100000777` | Cuenta EUR inactiva, tarjeta pendiente y préstamo vencido. |
| `00100000500` | Error 500. |
| `00100000504` | Error 504 tras 30 segundos configurables. |
| Otro | Error 404. |

Los fixtures se vuelven a leer en cada consulta: edite un JSON y repita la petición sin reiniciar. Los nombres de producto y montos son ficticios.

## Trazabilidad

La evidencia de implementación y verificación se registra en [BITACORA_CAMBIOS.md](BITACORA_CAMBIOS.md). Los documentos fuente no se versionan porque contienen valores externos no requeridos por el mock.
