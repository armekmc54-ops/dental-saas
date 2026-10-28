# Pull Request: Sistema Integral de Gestión Dental (SaaS) & Odontograma FDI

## 📌 Resumen de Cambios
Esta entrega deja el software dental 100% listo para demostraciones comerciales en vivo ante clínicas dentales y especialistas odontólogos, integrando:

### 1. ⏱️ Gestión y Bloqueo de Horarios en Tiempo Real
- **Bloqueo de Horarios para Hoy y Fechas Futuras (`POST /api/dentists/block-slot`):** Permite a recepcionistas y doctores bloquear rangos específicos (por cirugías de urgencia, permisos o reuniones) que se sustraen inmediatamente de la disponibilidad pública vía SignalR.
- **Liberación de Horarios en 1-Clic (`DELETE /api/dentists/exceptions/{id}`):** Restaura de inmediato el espacio liberado para nuevas citas.
- **Soporte de Agendamiento Inmediato para Hoy:** Eliminada la discrepancia de zona horaria (`DateTime.Now` vs `DateTime.UtcNow`) para garantizar que la clínica siempre cuente con cupos disponibles de demostración durante el día.

### 2. 💺 Flujo de Recepción y Sala de Espera en Vivo
- **Gran Banner Clínico:** Muestra en tiempo real quién está en sillón (con cronómetro de atención), quién está en sala de espera y quién es el siguiente paciente en turno por su nombre.
- **SignalR & Web Audio API:** Alertas sonoras y visuales cuando un paciente llega o es llamado al sillón.
- **Regla Estricta 24h/15m:** Cancelaciones bloqueadas con menos de 24h y confirmación requerida hasta 15 min antes.

### 3. 🦷 Odontograma FDI Vectorial SVG Reparado y Profesional
- **Solución de Coloreado y Borrado:** Herramienta **⚪ Sano (Borrar / Sanar)** para limpiar superficies o piezas completas.
- **5 Caras Anatómicas FDI:** 32 piezas adultas y 20 infantiles con cálculo automático de presupuesto en tiempo real.

### 4. 📁 Expedientes por Folio, Migración Papel y Asistente IA
- **Folios Numéricos Únicos (`FOLIO-00001` al `N`):** Con buscador y digitalización de expedientes físicos en PDF.
- **Asistente IA Clínico Certificado (Cero Alucinaciones):** Validación determinista anclada a la base de datos que detecta contraindicaciones fatales (ej. prohibición absoluta de Amoxicilina en pacientes alérgicos a Penicilina).

### 5. 🚀 Despliegue en la Nube y Base de Datos
- **Base de Datos:** Scripts SQL completos listos para ejecutar en PostgreSQL/Supabase (`database/schema_postgresql.sql`) y SQL Server (`database/schema_sqlserver.sql`).
- **Frontend Vercel:** Archivo `vercel.json` configurado para enrutamiento SPA y proxy de APIs.
- **Backend Docker:** `Dockerfile` multi-stage para despliegue en Railway, Render o Azure.

## 🧪 Pruebas Ejecutadas
- 5/5 pruebas unitarias automatizadas aprobadas (`AvailabilityEngineTests`, `OdontogramModelTests`).
- Verificación de API en vivo para disponibilidad, bloqueo y liberación de slots hoy, auditoría IA y subida de PDFs.
