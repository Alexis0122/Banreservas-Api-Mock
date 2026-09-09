# Bitácora de cambios

## MC-20260909-001

- **Objetivo:** mock local .NET 8 para autenticación y posición consolidada.
- **Estado inicial → final:** pendiente → validado localmente.
- **Alcance implementado:** endpoints, fixtures recargables, documentación y verificación reproducible.
- **Decisiones:** credenciales/tokens ficticios, sin paquetes externos; HTTP:9464 predeterminado y HTTPS:9465 opcional con dev-cert.
- **Pruebas ejecutadas:** `dotnet build` y `powershell -ExecutionPolicy Bypass -File .\\verify.ps1`.
- **Resultado:** ambas completaron correctamente; compilación con 0 advertencias y 0 errores.
- **Cobertura observada:** auth, token ausente/vencido, respuesta base, fixtures vacío/extendido/bordes, fecha inválida, 404, 500, 504, demora y formato de TRNID.
- **Incidente de entorno:** el sandbox no pudo leer el NuGet.Config del perfil; la compilación validada se ejecutó con acceso local aprobado.
- **Riesgo residual:** HTTPS requiere instalar un certificado de desarrollo; no se publican los documentos fuente que contienen valores externos.
- **Commit principal:** `f2f537c feat: add Banreservas API mock`.
- **Publicación bloqueada:** GitHub CLI no tiene una sesión válida para `Alexis0122`; el flujo web requiere que el titular complete la autenticación. El repositorio privado y el `push` se ejecutarán al renovarla.
