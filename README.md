# Apparka Training Flow — MVP para Visual Studio

Aplicación PWA mobile-first desarrollada con Blazor WebAssembly y .NET 8. Reemplaza los seis videos del periodo de entrenamiento por seis validaciones procedurales con QR, tarea práctica, rúbrica, microevaluación, confirmaciones y auditoría.

## Funcionalidades incluidas

- Plan automático de 3 semanas y 6 validaciones.
- Bloqueo secuencial: una validación no se habilita hasta completar la anterior.
- QR obligatorio con lector de cámara y entrada manual de respaldo.
- Tarea práctica obligatoria.
- Rúbrica de cinco criterios, incluyendo criterios críticos.
- Observación obligatoria ante cumplimiento parcial o incumplimiento.
- Microevaluación automática con puntaje mínimo de 70%.
- Confirmación del tutor y del colaborador.
- Refuerzo cuando existe un criterio crítico o un puntaje insuficiente.
- Panel del supervisor basado en excepciones.
- Auditoría por validación.
- Persistencia local en el navegador.
- Manifest y service worker para instalación como PWA.
- Interfaz adaptada a celular y escritorio.

## Abrir en Visual Studio

1. Instala Visual Studio 2022 con la carga de trabajo **Desarrollo de ASP.NET y web**.
2. Instala el SDK de .NET 8 si Visual Studio no lo incluye.
3. Descomprime la carpeta.
4. Abre `ApparkaTrainingFlow.sln`.
5. Espera a que Visual Studio restaure los paquetes NuGet.
6. Selecciona el perfil `https`.
7. Ejecuta con `F5` o `Ctrl + F5`.

## Código QR de demostración

Usa este valor en la primera validación:

```text
APPARKA-SEDE-LIMA-001
```

La lectura con cámara usa la API `BarcodeDetector`. Si el navegador no la soporta, se puede ingresar el código manualmente.

## Flujo para probar

1. Abre **Actividad**.
2. Ingresa a la validación 1.
3. Valida el QR.
4. Marca la tarea como ejecutada.
5. Completa la rúbrica.
6. Responde las tres preguntas.
7. Activa las dos confirmaciones.
8. Cierra la validación.
9. Comprueba que la validación 2 se desbloquea.

Para probar un refuerzo, marca **No cumple** en un criterio crítico o responde incorrectamente dos preguntas.

## Estructura principal

```text
ApparkaTrainingFlow/
├── Layout/
├── Models/
├── Pages/
├── Services/
├── wwwroot/
├── App.razor
├── Program.cs
└── ApparkaTrainingFlow.csproj
```

## Alcance del MVP

Este proyecto funciona sin servidor y guarda la información en `localStorage`. Es apropiado para demostración, validación del flujo y pruebas con usuarios.

Para producción se debe incorporar:

- API ASP.NET Core.
- SQL Server o PostgreSQL.
- Autenticación corporativa.
- Autorización por roles.
- QR firmado y de un solo uso.
- Registro de sede y ubicación validado desde el servidor.
- Auditoría inmutable.
- Notificaciones.
- Cifrado y políticas de retención.
- Pruebas unitarias, de integración y seguridad.

## Reiniciar la demostración

En la aplicación abre **Perfil** y selecciona **Restablecer datos**.
