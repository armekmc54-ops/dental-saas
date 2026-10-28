// DentalCare SaaS - Frontend Engine (Mobile-First, SignalR, FDI Odontogram & AI)

const AppState = {
  currentView: 'reception', // Default vista de demostración
  services: [],
  dentists: [],
  dentalChairs: [],
  
  // Wizard State
  bookingStep: 1,
  selectedService: null,
  selectedDentist: null, // null = cualquier doctor disponible
  selectedDate: new Date().toISOString().split('T')[0],
  selectedSlot: null,
  patientForm: {
    firstName: '',
    lastName: '',
    phone: '',
    curp: '',
    email: '',
    hasMedicalAlert: false,
    medicalAlertDescription: '',
    hasPain: false,
    painLevel: 5,
    bookingNotes: '',
    requiresInvoice: false,
    taxId: '',
    legalName: '',
    taxRegime: '605',
    taxZipCode: '',
    cfdiUsage: 'D01'
  },
  lastBookedResult: null,

  // Reception State
  kanbanAppointments: [],
  currentMagicToken: null,

  // Doctor & Folio State
  activeFolio: 'FOLIO-00001',
  activePatient: null,
  odontogramType: 'adult', // 'adult' (32) o 'pediatric' (20)
  activePaletteTool: 'Caries', // 'Healthy', 'Caries', 'Resin', 'Crown', 'Endodontics', 'Extraction', 'Missing'
  activeTreatmentStatus: 2, // 1: Preexistente, 2: Plan de Tratamiento (Presupuesto)
  toothStates: {}, // key: `${toothNumber}_${surface}` -> { condition, status, price }
  wholeToothStates: {}, // key: toothNumber -> { condition, status, price }
  evolutionNotes: '',
  patientDocuments: [],
  patientPrescriptions: []
};

// ==========================================
// 1. SIGNALR EN TIEMPO REAL & RETROALIMENTACIÓN
// ==========================================
let signalrConnection = null;

function initSignalR() {
  if (typeof signalR === 'undefined') {
    setTimeout(initSignalR, 1000);
    return;
  }

  signalrConnection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/clinic")
    .withAutomaticReconnect()
    .build();

  signalrConnection.on("PatientArrivedInWaitingRoom", (alert) => {
    playMedicalChime();
    triggerHaptic();
    showToastNotification(`🔔 ${alert.patientName} ha llegado a Sala de Espera (${alert.dentalChairName})`, 'warning');
    loadKanbanData();
  });

  signalrConnection.on("PatientCalledToChair", (alert) => {
    triggerHaptic();
    showToastNotification(`💺 ${alert.patientName} en consulta con ${alert.doctorName} en ${alert.chairName}`, 'info');
    loadKanbanData();
  });

  signalrConnection.on("AppointmentStatusChanged", (payload) => {
    loadKanbanData();
  });

  signalrConnection.on("NewAppointmentCreated", (payload) => {
    playMedicalChime();
    triggerHaptic();
    showToastNotification(`📅 Nueva cita agendada: ${payload.patientName} para ${payload.serviceName}`, 'success');
    loadKanbanData();
  });

  signalrConnection.on("AvailabilityChanged", (payload) => {
    triggerHaptic();
    showToastNotification(`⏱️ Agenda médica actualizada (${payload.action === 'Blocked' ? 'Horario bloqueado' : 'Horario liberado'})`, 'info');
    loadDoctorScheduleExceptions();
    if (AppState.bookingStep === 3) {
      loadAvailableSlots();
    }
  });

  signalrConnection.start()
    .then(() => {
      const el = document.getElementById("signalr-status");
      if (el) {
        el.innerHTML = `
          <span class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
            <span class="w-1.5 h-1.5 mr-1 bg-emerald-500 rounded-full animate-ping"></span>
            En Vivo
          </span>
        `;
      }
    })
    .catch(err => {
      console.warn("SignalR fallback local:", err);
    });
}

function playMedicalChime() {
  try {
    const AudioContext = window.AudioContext || window.webkitAudioContext;
    if (!AudioContext) return;
    const ctx = new AudioContext();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();

    osc.type = 'sine';
    osc.frequency.setValueAtTime(880, ctx.currentTime);
    osc.frequency.exponentialRampToValueAtTime(1320, ctx.currentTime + 0.3);

    gain.gain.setValueAtTime(0.12, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.7);

    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.start();
    osc.stop(ctx.currentTime + 0.7);
  } catch (e) {}
}

function triggerHaptic() {
  if (navigator.vibrate) {
    navigator.vibrate(40);
  }
}

function showToastNotification(message, type = 'info') {
  const container = document.getElementById("toast-container");
  if (!container) return;

  const toast = document.createElement("div");
  const bgColors = {
    info: 'bg-sky-600 text-white',
    success: 'bg-emerald-600 text-white',
    warning: 'bg-amber-500 text-white',
    error: 'bg-rose-600 text-white'
  };

  toast.className = `p-3 rounded-xl shadow-xl flex items-center gap-2.5 transition-all transform duration-200 translate-y-2 opacity-0 text-xs font-bold ${bgColors[type] || bgColors.info}`;
  toast.innerHTML = `<span>${message}</span>`;
  container.appendChild(toast);

  setTimeout(() => {
    toast.classList.remove('translate-y-2', 'opacity-0');
  }, 10);

  setTimeout(() => {
    toast.classList.add('opacity-0', 'translate-y-2');
    setTimeout(() => toast.remove(), 250);
  }, 4200);
}

// ==========================================
// 2. INICIALIZACIÓN
// ==========================================
document.addEventListener('DOMContentLoaded', async () => {
  initSignalR();
  await loadInitialMetadata();
  setupEventListeners();
  switchView('reception'); // Mostrar pantalla de recepción por defecto
  setInterval(updateLiveCounters, 1000); // Reloj de consulta en vivo
});

async function loadInitialMetadata() {
  try {
    const [servicesRes, dentistsRes, chairsRes] = await Promise.all([
      fetch('/api/services'),
      fetch('/api/dentists'),
      fetch('/api/dental-chairs')
    ]);

    AppState.services = await servicesRes.json();
    AppState.dentists = await dentistsRes.json();
    AppState.dentalChairs = await chairsRes.json();

    // Llenar selectores administrativos
    const quickSvc = document.getElementById('admin-quick-service');
    if (quickSvc) {
      quickSvc.innerHTML = AppState.services.map(s => `<option value="${s.id}">${s.name} ($${s.price})</option>`).join('');
    }
  } catch (err) {
    console.error("Error cargando metadatos:", err);
  }
}

function setupEventListeners() {
  // Theme Toggle
  const darkToggle = document.getElementById('theme-toggle');
  if (darkToggle) {
    darkToggle.addEventListener('click', () => {
      document.documentElement.classList.toggle('dark');
    });
  }
}

function switchView(viewName) {
  AppState.currentView = viewName;
  triggerHaptic();

  const views = ['booking', 'reception', 'doctor', 'admin', 'patient-portal'];
  views.forEach(v => {
    const el = document.getElementById(`view-${v}`);
    if (el) el.classList.toggle('hidden', v !== viewName);
  });

  // Resaltar tab activo
  document.querySelectorAll('.nav-tab').forEach(tab => {
    const isTarget = tab.getAttribute('data-view') === viewName;
    tab.classList.toggle('bg-white', isTarget);
    tab.classList.toggle('dark:bg-slate-700', isTarget);
    tab.classList.toggle('text-sky-600', isTarget);
    tab.classList.toggle('shadow-sm', isTarget);
  });

  if (viewName === 'booking') renderBookingWizard();
  if (viewName === 'reception') loadKanbanData();
  if (viewName === 'doctor') loadPatientByFolio(AppState.activeFolio);
  if (viewName === 'admin') loadAdminPanel();
  if (viewName === 'patient-portal') loadPatientPortalView('FOLIO-00003');
}

// ==========================================
// 3. WIZARD DE AGENDAMIENTO CON PASO ATRÁS
// ==========================================
function goToBookingStep(step) {
  AppState.bookingStep = step;
  triggerHaptic();
  renderBookingWizard();
}

function renderBookingWizard() {
  const stepperContainer = document.getElementById('wizard-stepper');
  if (stepperContainer) {
    const steps = [
      { step: 1, title: 'Tratamiento' },
      { step: 2, title: 'Especialista' },
      { step: 3, title: 'Fecha y Hora' },
      { step: 4, title: 'Datos' }
    ];
    stepperContainer.innerHTML = steps.map(s => {
      const isCompleted = AppState.bookingStep > s.step;
      const isCurrent = AppState.bookingStep === s.step;
      return `
        <div class="flex items-center cursor-pointer" onclick="goToBookingStep(${s.step})">
          <div class="flex items-center justify-center w-7 h-7 sm:w-8 sm:h-8 rounded-full text-xs font-bold transition-all ${
            isCompleted ? 'bg-emerald-600 text-white' : (isCurrent ? 'bg-sky-600 text-white ring-4 ring-sky-100 dark:ring-sky-950' : 'bg-slate-200 dark:bg-slate-800 text-slate-500')
          }">
            ${isCompleted ? '✓' : s.step}
          </div>
          <span class="ml-1.5 sm:ml-2 text-[11px] sm:text-xs font-bold ${isCurrent ? 'text-sky-600 dark:text-sky-400' : 'text-slate-500'}">
            ${s.title}
          </span>
          ${s.step < 4 ? `<div class="w-4 sm:w-8 h-0.5 mx-1.5 bg-slate-200 dark:bg-slate-800"></div>` : ''}
        </div>
      `;
    }).join('');
  }

  for (let i = 1; i <= 4; i++) {
    const el = document.getElementById(`wizard-step-${i}`);
    if (el) el.classList.toggle('hidden', AppState.bookingStep !== i);
  }

  const successEl = document.getElementById('wizard-step-success');
  if (successEl) successEl.classList.add('hidden');

  if (AppState.bookingStep === 1) renderStep1Services();
  if (AppState.bookingStep === 2) renderStep2Dentists();
  if (AppState.bookingStep === 3) renderStep3CalendarAndSlots();
  if (AppState.bookingStep === 4) renderStep4PatientDetails();
}

function renderStep1Services() {
  const container = document.getElementById('services-grid');
  if (!container) return;

  container.innerHTML = AppState.services.map(svc => {
    const isSelected = AppState.selectedService?.id === svc.id;
    return `
      <div onclick="selectService(${svc.id})" class="cursor-pointer p-4 rounded-2xl border-2 transition-all card-hover ${
        isSelected ? 'border-sky-500 bg-sky-50/50 dark:bg-sky-950/30 ring-2 ring-sky-500' : 'border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900'
      }">
        <div class="flex items-start justify-between">
          <span class="inline-flex items-center px-2 py-0.5 rounded-md text-[10px] font-extrabold" style="background-color: ${svc.badgeColor || '#10B981'}20; color: ${svc.badgeColor || '#10B981'}">
            ${svc.category}
          </span>
          <span class="text-lg font-black text-slate-900 dark:text-white">$${svc.price.toFixed(2)}</span>
        </div>
        <h4 class="mt-2 text-sm font-extrabold text-slate-900 dark:text-white">${svc.name}</h4>
        <p class="mt-1 text-[11px] text-slate-500 dark:text-slate-400 line-clamp-2">${svc.description || ''}</p>
        <div class="mt-3 pt-2.5 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between text-[11px] text-slate-400 font-medium">
          <span>⏱️ ${svc.durationMinutes} min</span>
          <span>+${svc.bufferTimeMinutes}m desinfección</span>
        </div>
      </div>
    `;
  }).join('');
}

function selectService(serviceId) {
  AppState.selectedService = AppState.services.find(s => s.id === serviceId);
  goToBookingStep(2);
}

function renderStep2Dentists() {
  const container = document.getElementById('dentists-grid');
  if (!container) return;

  const isAnySelected = AppState.selectedDentist === null;
  let html = `
    <div onclick="selectDentist(null)" class="cursor-pointer p-4 rounded-2xl border-2 transition-all card-hover ${
      isAnySelected ? 'border-sky-500 bg-sky-50/50 dark:bg-sky-950/30 ring-2 ring-sky-500' : 'border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900'
    }">
      <div class="flex items-center gap-3.5">
        <div class="w-12 h-12 rounded-2xl bg-gradient-to-tr from-sky-500 to-indigo-600 flex items-center justify-center text-white text-lg font-bold shadow-md">
          ⚡
        </div>
        <div>
          <span class="inline-block px-1.5 py-0.5 rounded text-[10px] font-extrabold bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
            Recomendado
          </span>
          <h4 class="text-sm font-bold text-slate-900 dark:text-white mt-0.5">Cualquier doctor disponible</h4>
          <p class="text-[11px] text-slate-500">Asignación inteligente sin esperas</p>
        </div>
      </div>
    </div>
  `;

  html += AppState.dentists.map(d => {
    const isSelected = AppState.selectedDentist?.id === d.id;
    return `
      <div onclick="selectDentist(${d.id})" class="cursor-pointer p-4 rounded-2xl border-2 transition-all card-hover ${
        isSelected ? 'border-sky-500 bg-sky-50/50 dark:bg-sky-950/30 ring-2 ring-sky-500' : 'border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900'
      }">
        <div class="flex items-center gap-3.5">
          <div class="w-12 h-12 rounded-2xl flex items-center justify-center text-white font-bold text-base shadow" style="background-color: ${d.calendarColorHex || '#2563EB'}">
            ${d.fullName.substring(0, 2).toUpperCase()}
          </div>
          <div>
            <h4 class="text-sm font-bold text-slate-900 dark:text-white">${d.fullName}</h4>
            <p class="text-[11px] font-semibold text-sky-600">${d.specialty}</p>
            <p class="text-[10px] text-slate-400">Cédula: ${d.licenseNumber}</p>
          </div>
        </div>
      </div>
    `;
  }).join('');

  container.innerHTML = html;
}

function selectDentist(dentistId) {
  AppState.selectedDentist = dentistId ? AppState.dentists.find(d => d.id === dentistId) : null;
  goToBookingStep(3);
}

async function renderStep3CalendarAndSlots() {
  const dateInput = document.getElementById('booking-date-picker');
  if (dateInput) {
    dateInput.value = AppState.selectedDate;
    dateInput.min = new Date().toISOString().split('T')[0];
    dateInput.onchange = (e) => {
      AppState.selectedDate = e.target.value;
      loadAvailableSlots();
    };
  }
  await loadAvailableSlots();
}

async function loadAvailableSlots() {
  const morningContainer = document.getElementById('slots-morning');
  const afternoonContainer = document.getElementById('slots-afternoon');
  const waitlistBanner = document.getElementById('waitlist-banner');
  if (!morningContainer || !afternoonContainer) return;

  morningContainer.innerHTML = `<div class="p-3 text-xs text-slate-400 animate-pulse">Consultando disponibilidad...</div>`;
  afternoonContainer.innerHTML = '';
  if (waitlistBanner) waitlistBanner.classList.add('hidden');

  try {
    const dentistParam = AppState.selectedDentist ? `&dentistId=${AppState.selectedDentist.id}` : '';
    const res = await fetch(`/api/availability?date=${AppState.selectedDate}&serviceId=${AppState.selectedService.id}${dentistParam}`);
    const data = await res.json();

    if (data.isDayFull || data.totalAvailableSlots === 0) {
      morningContainer.innerHTML = `<p class="text-xs text-slate-500 py-2 italic">Sin cupos matutinos.</p>`;
      afternoonContainer.innerHTML = `<p class="text-xs text-slate-500 py-2 italic">Sin cupos vespertinos.</p>`;
      if (waitlistBanner) waitlistBanner.classList.remove('hidden');
      return;
    }

    morningContainer.innerHTML = data.morningSlots.length > 0
      ? data.morningSlots.map(s => renderSlotPill(s)).join('')
      : `<p class="text-[11px] text-slate-400 italic">No hay cupos en la mañana</p>`;

    afternoonContainer.innerHTML = data.afternoonSlots.length > 0
      ? data.afternoonSlots.map(s => renderSlotPill(s)).join('')
      : `<p class="text-[11px] text-slate-400 italic">No hay cupos en la tarde</p>`;
  } catch (err) {
    morningContainer.innerHTML = `<p class="text-xs text-rose-500">Error al cargar horarios.</p>`;
  }
}

function renderSlotPill(slot) {
  const isSelected = AppState.selectedSlot?.startDateTime === slot.startDateTime;
  const timeFormatted = new Date(slot.startDateTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  const jsonSlot = JSON.stringify(slot).replace(/"/g, '&quot;');
  return `
    <button type="button" onclick="selectSlot('${jsonSlot}')" class="px-3.5 py-2 rounded-xl text-xs font-bold transition-all border ${
      isSelected ? 'bg-sky-600 text-white border-sky-600 shadow-md ring-2 ring-sky-300' : 'bg-white dark:bg-slate-900 text-slate-800 dark:text-slate-200 border-slate-200 dark:border-slate-800 hover:border-sky-500'
    }">
      ${timeFormatted}
      <span class="block text-[9px] font-normal opacity-75">${slot.dentistName.split(' ')[1] || 'Dr'}</span>
    </button>
  `;
}

function selectSlot(slotJson) {
  AppState.selectedSlot = JSON.parse(slotJson);
  goToBookingStep(4);
}

function renderStep4PatientDetails() {
  const summaryEl = document.getElementById('booking-summary-badge');
  if (summaryEl && AppState.selectedSlot) {
    const slotDate = new Date(AppState.selectedSlot.startDateTime);
    summaryEl.innerHTML = `
      <div class="p-3.5 rounded-xl bg-slate-50 dark:bg-slate-800/60 border border-slate-200 dark:border-slate-700 flex items-center justify-between">
        <div>
          <span class="text-[10px] font-bold text-sky-600 uppercase tracking-wider">${AppState.selectedService.name}</span>
          <p class="text-xs font-bold text-slate-900 dark:text-white mt-0.5">
            📅 ${slotDate.toLocaleDateString('es-ES', { weekday: 'short', day: 'numeric', month: 'short' })} a las ${slotDate.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
          </p>
          <p class="text-[10px] text-slate-400">${AppState.selectedSlot.dentistName} • ${AppState.selectedSlot.dentalChairName}</p>
        </div>
        <div class="text-right">
          <span class="text-base font-black text-slate-900 dark:text-white">$${AppState.selectedService.price.toFixed(2)}</span>
          <span class="block text-[10px] text-emerald-600 font-semibold">Pago en clínica</span>
        </div>
      </div>
    `;
  }
}

async function submitBookingForm(e) {
  e.preventDefault();
  const btn = document.getElementById('btn-submit-booking');
  if (btn) btn.disabled = true;

  const payload = {
    serviceId: AppState.selectedService.id,
    preferredDentistId: AppState.selectedDentist ? AppState.selectedDentist.id : AppState.selectedSlot.dentistId,
    startDateTime: AppState.selectedSlot.startDateTime,
    firstName: document.getElementById('form-first-name').value,
    lastName: document.getElementById('form-last-name').value,
    phoneNumber: document.getElementById('form-phone').value,
    curp: document.getElementById('form-curp').value,
    email: document.getElementById('form-email').value,
    requiresInvoice: document.getElementById('form-requires-invoice').checked,
    taxId: document.getElementById('form-tax-id')?.value || '',
    legalName: document.getElementById('form-legal-name')?.value || '',
    taxZipCode: document.getElementById('form-tax-zip')?.value || '',
    taxRegime: document.getElementById('form-tax-regime')?.value || '605',
    cfdiUsage: document.getElementById('form-cfdi-usage')?.value || 'D01',
    hasMedicalAlertOrAllergy: document.getElementById('form-has-allergy').checked,
    medicalAlertDescription: document.getElementById('form-allergy-desc').value,
    hasCurrentPain: document.getElementById('form-has-pain').checked,
    painLevel: parseInt(document.getElementById('form-pain-level').value || '0', 10)
  };

  try {
    const res = await fetch('/api/appointments/book', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json; charset=utf-8' },
      body: JSON.stringify(payload)
    });

    const result = await res.json();
    if (res.ok && result.success) {
      triggerHaptic();
      showBookingSuccessScreen(result);
    } else {
      alert(result.errorMessage || "Horario ocupado. Por favor selecciona otro.");
      goToBookingStep(3);
    }
  } catch (err) {
    alert("Error de conexión al procesar la cita.");
  } finally {
    if (btn) btn.disabled = false;
  }
}

function showBookingSuccessScreen(result) {
  for (let i = 1; i <= 4; i++) {
    const stepEl = document.getElementById(`wizard-step-${i}`);
    if (stepEl) stepEl.classList.add('hidden');
  }

  const successEl = document.getElementById('wizard-step-success');
  if (successEl) {
    successEl.classList.remove('hidden');
    document.getElementById('success-code').textContent = result.appointmentCode;
    document.getElementById('success-folio').textContent = result.folioNumber || 'FOLIO-00004';
    document.getElementById('success-magic-url').href = result.magicManageUrl;
    document.getElementById('success-magic-url').textContent = `${window.location.origin}${result.magicManageUrl}`;
  }
}

// ==========================================
// 4. RECEPCIÓN Y FLUJO EN VIVO (BANNER Y KANBAN)
// ==========================================
async function loadKanbanData() {
  try {
    const res = await fetch('/api/appointments/kanban');
    AppState.kanbanAppointments = await res.json();
    renderKanbanAndLiveBanner();
  } catch (err) {
    console.error("Error cargando recepción:", err);
  }
}

function renderKanbanAndLiveBanner() {
  const confirmed = AppState.kanbanAppointments.filter(a => a.status === 'Scheduled' || a.status === 'Confirmed');
  const waiting = AppState.kanbanAppointments.filter(a => a.status === 'WaitingRoom');
  const inChair = AppState.kanbanAppointments.filter(a => a.status === 'InChair');
  const completed = AppState.kanbanAppointments.filter(a => a.status === 'Completed');

  // Actualizar Contadores
  document.getElementById('count-confirmed').textContent = confirmed.length;
  document.getElementById('count-waiting').textContent = waiting.length;
  document.getElementById('count-in-chair').textContent = inChair.length;
  document.getElementById('count-completed').textContent = completed.length;

  // Actualizar Gran Banner En Vivo
  // 1. En Consulta Ahora
  const chairDisplay = document.getElementById('live-in-chair-display');
  if (inChair.length > 0) {
    const active = inChair[0];
    chairDisplay.innerHTML = `
      <h4 class="text-base font-black truncate">${active.patientName}</h4>
      <p class="text-xs text-sky-100 font-medium">${active.dentistName} • ${active.dentalChairName}</p>
    `;
  } else {
    chairDisplay.innerHTML = `
      <h4 class="text-base font-black truncate">Sillón Disponible</h4>
      <p class="text-xs text-sky-100">Sin paciente en atención</p>
    `;
  }

  // 2. En Sala de Espera
  const waitingDisplay = document.getElementById('live-waiting-display');
  const waitingCount = document.getElementById('live-waiting-count');
  waitingCount.textContent = `${waiting.length} paciente${waiting.length === 1 ? '' : 's'}`;

  if (waiting.length > 0) {
    waitingDisplay.innerHTML = `
      <h4 class="text-base font-black truncate">${waiting.map(w => w.patientName).join(', ')}</h4>
      <p class="text-xs text-amber-100 font-medium">Llegaron a la clínica</p>
    `;
  } else {
    waitingDisplay.innerHTML = `
      <h4 class="text-base font-black truncate">Sala Despejada</h4>
      <p class="text-xs text-amber-100">Sin pacientes esperando</p>
    `;
  }

  // 3. Siguiente por su Nombre
  const nextDisplay = document.getElementById('live-next-display');
  const nextTime = document.getElementById('live-next-time');
  const nextCandidate = waiting.length > 0 ? waiting[0] : confirmed[0];

  if (nextCandidate) {
    const t = new Date(nextCandidate.startDateTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    nextTime.textContent = t;
    nextDisplay.innerHTML = `
      <h4 class="text-base font-black truncate">${nextCandidate.patientName}</h4>
      <p class="text-xs text-emerald-100 font-medium">${nextCandidate.serviceName}</p>
    `;
  } else {
    nextTime.textContent = '--:--';
    nextDisplay.innerHTML = `
      <h4 class="text-base font-black truncate">Nadie en fila</h4>
      <p class="text-xs text-emerald-100">Agenda libre</p>
    `;
  }

  // Render Columnas Kanban
  renderKanbanColumn('col-confirmed', confirmed);
  renderKanbanColumn('col-waiting', waiting);
  renderKanbanColumn('col-in-chair', inChair);
  renderKanbanColumn('col-completed', completed);
}

function renderKanbanColumn(elementId, appointments) {
  const col = document.getElementById(elementId);
  if (!col) return;
  col.innerHTML = '';

  if (appointments.length === 0) {
    col.innerHTML = `<p class="text-xs text-slate-400 py-4 text-center italic">Sin pacientes en este estado</p>`;
    return;
  }

  appointments.forEach(apt => {
    const card = document.createElement('div');
    const timeFormatted = new Date(apt.startDateTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

    // Regla de confirmación a 15 min: si faltan menos de 15 min y no ha confirmado
    const now = new Date();
    const aptTime = new Date(apt.startDateTime);
    const diffMinutes = (aptTime - now) / 60000;
    const isOverdueConfirmation = (apt.status === 'Scheduled') && (diffMinutes <= 15);

    const alertBadge = apt.hasCriticalAlerts ? `
      <div class="mt-1.5 p-1.5 rounded-md bg-rose-50 text-rose-700 dark:bg-rose-950 dark:text-rose-300 text-[10px] font-black alert-pulse-critical border border-rose-300">
        ⚠️ ALERTA: ${apt.quickAllergyAlertNote || apt.criticalAlerts[0] || 'Condición Crítica'}
      </div>
    ` : '';

    let actionHtml = '';
    if (apt.status === 'Scheduled' || apt.status === 'Confirmed') {
      actionHtml = `
        <button onclick="checkInPatient(${apt.id})" class="w-full mt-2.5 px-3 py-1.5 rounded-xl bg-sky-600 hover:bg-sky-700 text-white text-xs font-bold transition flex items-center justify-center gap-1 shadow-sm">
          <span>Marcar Llegada a Sala</span>
        </button>
      `;
      if (isOverdueConfirmation) {
        actionHtml += `
          <button onclick="enforceConfirmationOverdue(${apt.id})" class="w-full mt-1.5 px-2 py-1 rounded-lg bg-rose-100 text-rose-800 text-[10px] font-extrabold hover:bg-rose-200">
            ⚠️ No confirmó a tiempo (Reagendar / Cancelar)
          </button>
        `;
      }
    } else if (apt.status === 'WaitingRoom') {
      actionHtml = `
        <button onclick="callToChair(${apt.id})" class="w-full mt-2.5 px-3 py-1.5 rounded-xl bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-bold transition flex items-center justify-center gap-1 shadow-sm">
          <span>Llamar al Sillón</span>
        </button>
      `;
    } else if (apt.status === 'InChair') {
      actionHtml = `
        <div class="grid grid-cols-2 gap-1.5 mt-2.5">
          <button onclick="openDoctorByPatientId(${apt.patientId})" class="px-2 py-1.5 rounded-lg bg-indigo-50 text-indigo-700 dark:bg-indigo-950 dark:text-indigo-300 text-xs font-bold">
            Odontograma
          </button>
          <button onclick="completeAppointment(${apt.id})" class="px-2 py-1.5 rounded-lg bg-emerald-600 text-white text-xs font-bold">
            Cobrar & Fin
          </button>
        </div>
      `;
    } else {
      actionHtml = `<span class="mt-2 block text-center text-[10px] font-bold text-emerald-600">✓ Finalizado</span>`;
    }

    card.className = "p-3 rounded-2xl bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-sm space-y-1";
    card.innerHTML = `
      <div class="flex items-center justify-between text-[11px] font-bold">
        <span class="text-slate-500">${timeFormatted}</span>
        <span class="px-2 py-0.5 rounded-md" style="background-color: ${apt.dentistColorHex}20; color: ${apt.dentistColorHex}">
          ${apt.dentistName.split(' ')[1] || 'Dr'}
        </span>
      </div>
      <h5 class="text-xs font-extrabold text-slate-900 dark:text-white pt-0.5">${apt.patientName}</h5>
      <p class="text-[11px] text-slate-400">${apt.serviceName} • 💺 ${apt.dentalChairName}</p>
      ${alertBadge}
      ${actionHtml}
    `;
    col.appendChild(card);
  });
}

function updateLiveCounters() {
  const timerEl = document.getElementById('live-in-chair-time');
  if (!timerEl) return;
  const inChair = AppState.kanbanAppointments.find(a => a.status === 'InChair');
  if (inChair && inChair.inChairCheckInAt) {
    const elapsed = Math.floor((new Date() - new Date(inChair.inChairCheckInAt)) / 1000);
    const m = Math.floor(elapsed / 60);
    const s = elapsed % 60;
    timerEl.textContent = `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  } else {
    timerEl.textContent = '00:00';
  }
}

async function checkInPatient(appointmentId) {
  triggerHaptic();
  try {
    const res = await fetch(`/api/appointments/${appointmentId}/checkin`, { method: 'POST' });
    if (res.ok) {
      showToastNotification("Paciente en Sala de Espera. Alerta emitida.", "success");
      loadKanbanData();
    }
  } catch (err) {}
}

async function callToChair(appointmentId) {
  triggerHaptic();
  try {
    const res = await fetch(`/api/appointments/${appointmentId}/calltochair`, { method: 'POST' });
    if (res.ok) {
      loadKanbanData();
    }
  } catch (err) {}
}

async function completeAppointment(appointmentId) {
  triggerHaptic();
  if (!confirm("¿Deseas finalizar la consulta y registrar el cobro del tratamiento?")) return;
  try {
    const res = await fetch(`/api/appointments/${appointmentId}/complete`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ amount: 0, paymentMethod: 1 })
    });
    if (res.ok) {
      showToastNotification("Cita completada y cobrada con éxito.", "success");
      loadKanbanData();
    }
  } catch (err) {}
}

async function enforceConfirmationOverdue(appointmentId) {
  triggerHaptic();
  if (!confirm("Esta cita superó el límite de confirmación (máx 15 min antes). ¿Poner en cola de reagendamiento?")) return;
  try {
    const res = await fetch(`/api/appointments/${appointmentId}/enforce-confirmation`, { method: 'POST' });
    const data = await res.json();
    showToastNotification(data.message, "warning");
    loadKanbanData();
  } catch (err) {}
}

// ==========================================
// 5. EXPEDIENTE POR FOLIO & ODONTOGRAMA FDI REPARADO
// ==========================================
async function searchPatientByFolio() {
  const input = document.getElementById('input-search-folio');
  if (!input || !input.value.trim()) return;
  await loadPatientByFolio(input.value.trim());
}

function openDoctorByPatientId(patientId) {
  const apt = AppState.kanbanAppointments.find(a => a.patientId === patientId);
  const folio = apt ? `FOLIO-${patientId.toString().padStart(5, '0')}` : 'FOLIO-00001';
  switchView('doctor');
  loadPatientByFolio(folio);
}

async function loadPatientByFolio(folio) {
  AppState.activeFolio = folio;
  try {
    const res = await fetch(`/api/patients/by-folio/${folio}`);
    if (!res.ok) {
      alert(`No se encontró ningún expediente con el folio: ${folio}`);
      return;
    }

    const patient = await res.json();
    AppState.activePatient = patient;

    document.getElementById('patient-folio-badge').textContent = patient.folioNumber;
    document.getElementById('patient-name-header').textContent = patient.fullName;
    document.getElementById('patient-curp-header').textContent = `CURP: ${patient.curp || 'No registrada'}`;

    // Semáforo Médico en Cabecera
    const alertBar = document.getElementById('doctor-alert-bar');
    if (alertBar) {
      if (patient.medicalAlerts.length > 0) {
        alertBar.classList.remove('hidden');
        alertBar.innerHTML = `
          <div class="p-3.5 rounded-2xl bg-rose-50 dark:bg-rose-950/40 border border-rose-300 dark:border-rose-800 flex items-center justify-between text-rose-800 dark:text-rose-200">
            <div class="flex items-center gap-2">
              <span class="w-3 h-3 rounded-full bg-rose-600 animate-ping"></span>
              <span class="text-xs font-black uppercase tracking-wider">Alertas Médicas del Folio:</span>
              <span class="text-xs font-bold">${patient.medicalAlerts.map(m => m.description).join(' • ')}</span>
            </div>
            <span class="px-2 py-0.5 rounded bg-rose-200 dark:bg-rose-900 text-rose-900 dark:text-rose-100 text-[11px] font-extrabold uppercase">Crítico</span>
          </div>
        `;
      } else {
        alertBar.classList.add('hidden');
      }
    }

    // Cargar Documentos y Recetas
    renderPatientDocuments(patient.documents || []);
    renderPatientPrescriptions(patient.prescriptions || []);

    // Cargar Odontograma del paciente
    await loadPatientOdontogram(patient.id);
    renderSvgOdontogram();
  } catch (err) {
    console.error("Error al cargar expediente por folio:", err);
  }
}

function renderPatientDocuments(docs) {
  const container = document.getElementById('patient-documents-list');
  if (!container) return;
  if (docs.length === 0) {
    container.innerHTML = `<p class="text-[11px] text-slate-400 italic">Sin documentos escaneados aún</p>`;
    return;
  }
  container.innerHTML = docs.map(d => `
    <div class="flex items-center justify-between p-2 rounded-lg bg-slate-50 dark:bg-slate-800 text-[11px]">
      <span class="font-bold text-slate-700 dark:text-slate-300 truncate max-w-[140px]">${d.title}</span>
      <a href="${d.relativePath}" target="_blank" class="text-sky-600 hover:underline font-extrabold">Ver PDF</a>
    </div>
  `).join('');
}

function renderPatientPrescriptions(prescriptions) {
  const container = document.getElementById('patient-prescriptions-list');
  if (!container) return;
  if (prescriptions.length === 0) {
    container.innerHTML = `<p class="text-[11px] text-slate-400 italic">Sin recetas registradas</p>`;
    return;
  }
  container.innerHTML = prescriptions.map(pr => `
    <div class="p-2 rounded-lg bg-slate-50 dark:bg-slate-800 text-[11px] space-y-1">
      <div class="flex justify-between font-bold text-sky-600">
        <span>${pr.prescriptionFolio}</span>
        <span class="text-slate-400">${new Date(pr.createdAt).toLocaleDateString()}</span>
      </div>
      <p class="text-slate-700 dark:text-slate-300 font-semibold">${pr.diagnosis}</p>
      <div class="text-[10px] text-slate-500">
        ${pr.items.map(i => `• ${i.medicineName} (${i.dosage})`).join('<br>')}
      </div>
    </div>
  `).join('');
}

async function uploadPatientDocument(input) {
  if (!input.files || input.files.length === 0) return;
  const file = input.files[0];
  const formData = new FormData();
  formData.append('file', file);
  formData.append('title', file.name);
  formData.append('documentType', 'HistorialPapelEscaneado');

  try {
    const res = await fetch(`/api/patients/${AppState.activeFolio}/documents`, {
      method: 'POST',
      body: formData
    });
    if (res.ok) {
      showToastNotification("Documento digitalizado y vinculado al Folio.", "success");
      loadPatientByFolio(AppState.activeFolio);
    }
  } catch (err) {
    alert("Error al subir archivo.");
  }
}

// ==========================================
// ODONTOGRAMA SVG (REPARADO Y PROFESIONAL)
// ==========================================
function setOdontogramType(type) {
  AppState.odontogramType = type;
  document.getElementById('btn-odo-adult').classList.toggle('text-sky-600', type === 'adult');
  document.getElementById('btn-odo-pediatric').classList.toggle('text-sky-600', type === 'pediatric');
  renderSvgOdontogram();
}

function setPaletteTool(toolName) {
  AppState.activePaletteTool = toolName;
  triggerHaptic();
  document.querySelectorAll('.palette-btn').forEach(btn => {
    btn.classList.toggle('ring-2', btn.getAttribute('data-tool') === toolName);
  });
}

async function loadPatientOdontogram(patientId) {
  try {
    const res = await fetch(`/api/patients/${patientId}/odontogram`);
    const data = await res.json();

    AppState.toothStates = {};
    AppState.wholeToothStates = {};

    if (data.toothSurfaces && data.toothSurfaces.length > 0) {
      data.toothSurfaces.forEach(ts => {
        if (ts.surface === 0) {
          AppState.wholeToothStates[ts.toothNumber] = {
            condition: getConditionName(ts.conditionType),
            status: ts.status,
            price: ts.estimatedPrice
          };
        } else {
          AppState.toothStates[`${ts.toothNumber}_${ts.surface}`] = {
            condition: getConditionName(ts.conditionType),
            status: ts.status,
            price: ts.estimatedPrice
          };
        }
      });
    }
    updateBudgetDisplay();
  } catch (err) {}
}

function getConditionName(type) {
  const map = {
    0: 'Healthy',
    1: 'Caries',
    2: 'Resin',
    3: 'Amalgam',
    4: 'Missing',
    5: 'Crown',
    6: 'Endodontics',
    8: 'Extraction'
  };
  return map[type] || 'Healthy';
}

function renderSvgOdontogram() {
  const container = document.getElementById('odontogram-svg-canvas');
  if (!container) return;

  const isAdult = AppState.odontogramType === 'adult';

  // Cuadrantes FDI
  const q1 = isAdult ? [18, 17, 16, 15, 14, 13, 12, 11] : [55, 54, 53, 52, 51];
  const q2 = isAdult ? [21, 22, 23, 24, 25, 26, 27, 28] : [61, 62, 63, 64, 65];
  const q4 = isAdult ? [48, 47, 46, 45, 44, 43, 42, 41] : [85, 84, 83, 82, 81];
  const q3 = isAdult ? [31, 32, 33, 34, 35, 36, 37, 38] : [71, 72, 73, 74, 75];

  container.innerHTML = `
    <div class="space-y-4 min-w-[620px]">
      <!-- Maxilar Superior -->
      <div>
        <div class="text-[10px] font-black uppercase text-center text-slate-400 tracking-widest mb-1.5">Maxilar Superior</div>
        <div class="flex justify-center items-center gap-1 sm:gap-2">
          <div class="flex gap-1 border-r-2 border-slate-300 dark:border-slate-700 pr-2">
            ${q1.map(t => renderToothComponent(t, 'upper')).join('')}
          </div>
          <div class="flex gap-1 pl-2">
            ${q2.map(t => renderToothComponent(t, 'upper')).join('')}
          </div>
        </div>
      </div>

      <!-- Línea Oclusal -->
      <div class="w-full h-0.5 bg-slate-200 dark:border-slate-800"></div>

      <!-- Mandíbula Inferior -->
      <div>
        <div class="flex justify-center items-center gap-1 sm:gap-2">
          <div class="flex gap-1 border-r-2 border-slate-300 dark:border-slate-700 pr-2">
            ${q4.map(t => renderToothComponent(t, 'lower')).join('')}
          </div>
          <div class="flex gap-1 pl-2">
            ${q3.map(t => renderToothComponent(t, 'lower')).join('')}
          </div>
        </div>
        <div class="text-[10px] font-black uppercase text-center text-slate-400 tracking-widest mt-1.5">Mandíbula Inferior</div>
      </div>
    </div>
  `;
}

function renderToothComponent(toothNumber, arch) {
  const whole = AppState.wholeToothStates[toothNumber];
  const isMissing = whole?.condition === 'Missing';
  const isExtraction = whole?.condition === 'Extraction';
  const isCrown = whole?.condition === 'Crown';

  const getColor = (surf) => {
    if (isMissing) return '#1e293b'; // Diente ausente = gris oscuro/negro
    if (isCrown) return '#f59e0b';   // Corona = dorado
    const st = AppState.toothStates[`${toothNumber}_${surf}`];
    if (!st || st.condition === 'Healthy') return '#ffffff';
    if (st.condition === 'Caries') return '#ef4444'; // Rojo
    if (st.condition === 'Resin') return '#2563eb';  // Azul
    if (st.condition === 'Endodontics') return '#8b5cf6'; // Morado
    return '#ffffff';
  };

  const cTop = getColor(1);
  const cCenter = getColor(5);
  const cBottom = getColor(2);
  const cLeft = getColor(3);
  const cRight = getColor(4);

  return `
    <div class="tooth-box flex flex-col items-center p-1 bg-white dark:bg-slate-800 rounded-xl border border-slate-200 dark:border-slate-700 shadow-sm">
      <span class="text-[10px] font-black text-slate-600 dark:text-slate-300 mb-0.5">${toothNumber}</span>
      
      <svg width="38" height="38" viewBox="0 0 100 100" class="overflow-visible">
        <!-- Cara 1: Vestibular/Superior -->
        <polygon points="0,0 100,0 80,25 20,25" fill="${cTop}" stroke="#94a3b8" stroke-width="2" class="tooth-surface" onclick="handleSurfaceClick(event, ${toothNumber}, 1)" />

        <!-- Cara 2: Palatino/Lingual -->
        <polygon points="20,75 80,75 100,100 0,100" fill="${cBottom}" stroke="#94a3b8" stroke-width="2" class="tooth-surface" onclick="handleSurfaceClick(event, ${toothNumber}, 2)" />

        <!-- Cara 3: Mesial -->
        <polygon points="0,0 20,25 20,75 0,100" fill="${cLeft}" stroke="#94a3b8" stroke-width="2" class="tooth-surface" onclick="handleSurfaceClick(event, ${toothNumber}, 3)" />

        <!-- Cara 4: Distal -->
        <polygon points="100,0 100,100 80,75 80,25" fill="${cRight}" stroke="#94a3b8" stroke-width="2" class="tooth-surface" onclick="handleSurfaceClick(event, ${toothNumber}, 4)" />

        <!-- Cara 5: Oclusal/Incisal -->
        <polygon points="20,25 80,25 80,75 20,75" fill="${cCenter}" stroke="#94a3b8" stroke-width="2" class="tooth-surface" onclick="handleSurfaceClick(event, ${toothNumber}, 5)" />

        ${isExtraction ? `
          <line x1="8" y1="8" x2="92" y2="92" stroke="#ef4444" stroke-width="6" stroke-linecap="round"/>
          <line x1="92" y1="8" x2="8" y2="92" stroke="#ef4444" stroke-width="6" stroke-linecap="round"/>
        ` : ''}
      </svg>

      <!-- Acción de Pieza Completa (Menú Rápido) -->
      <button type="button" onclick="openWholeToothMenu(${toothNumber})" class="mt-1 text-[8px] font-bold text-slate-400 hover:text-sky-600 transition">
        Pieza
      </button>
    </div>
  `;
}

// CLIC EN CARA DENTAL (REPARADO: BORRA O PINTA INMEDIATAMENTE)
function handleSurfaceClick(e, toothNumber, surfaceId) {
  e.stopPropagation();
  triggerHaptic();

  const tool = AppState.activePaletteTool;
  const status = AppState.activeTreatmentStatus; // 1: Preexistente, 2: Presupuesto
  const key = `${toothNumber}_${surfaceId}`;

  // Si la herramienta es Borrador / Sano -> Limpia la cara
  if (tool === 'Healthy') {
    delete AppState.toothStates[key];
  } else {
    // Precios estimados para presupuesto automático
    const prices = { Caries: 75.00, Resin: 0, Crown: 250.00, Endodontics: 220.00 };
    AppState.toothStates[key] = {
      condition: tool,
      status: status,
      price: status === 2 ? (prices[tool] || 0) : 0
    };
  }

  renderSvgOdontogram();
  updateBudgetDisplay();
}

function openWholeToothMenu(toothNumber) {
  triggerHaptic();
  const choice = prompt(`Pieza ${toothNumber} - Selecciona acción:\n1. ⚪ Sano / Borrar Todo el Diente\n2. 🟡 Corona Dental\n3. ❌ Extracción Indicada\n4. ⚫ Diente Ausente / Extraído`);
  if (!choice) return;

  if (choice === '1') {
    delete AppState.wholeToothStates[toothNumber];
    for (let s = 1; s <= 5; s++) delete AppState.toothStates[`${toothNumber}_${s}`];
  } else if (choice === '2') {
    AppState.wholeToothStates[toothNumber] = { condition: 'Crown', status: 2, price: 250.00 };
  } else if (choice === '3') {
    AppState.wholeToothStates[toothNumber] = { condition: 'Extraction', status: 2, price: 85.00 };
  } else if (choice === '4') {
    AppState.wholeToothStates[toothNumber] = { condition: 'Missing', status: 1, price: 0 };
  }

  renderSvgOdontogram();
  updateBudgetDisplay();
}

function updateBudgetDisplay() {
  let total = 0;
  const items = [];

  Object.entries(AppState.toothStates).forEach(([key, val]) => {
    if (val.status === 2 && val.price > 0) {
      total += val.price;
      const [t, s] = key.split('_');
      items.push({ tooth: t, condition: val.condition, price: val.price });
    }
  });

  Object.entries(AppState.wholeToothStates).forEach(([t, val]) => {
    if (val.status === 2 && val.price > 0) {
      total += val.price;
      items.push({ tooth: t, condition: val.condition, price: val.price });
    }
  });

  const totalEl = document.getElementById('budget-total-amount');
  if (totalEl) totalEl.textContent = `$${total.toFixed(2)}`;

  const itemsContainer = document.getElementById('budget-items-list');
  if (itemsContainer) {
    itemsContainer.innerHTML = items.length === 0
      ? `<p class="text-[11px] text-slate-400 italic">Sin tratamientos pendientes</p>`
      : items.map(i => `
          <div class="flex justify-between py-0.5 border-b border-slate-100 dark:border-slate-800">
            <span>Pieza ${i.tooth} (${i.condition})</span>
            <span class="font-bold">$${i.price.toFixed(2)}</span>
          </div>
        `).join('');
  }
}

async function saveOdontogramSnapshot() {
  triggerHaptic();
  const toothSurfaces = [];
  const map = { 'Healthy': 0, 'Caries': 1, 'Resin': 2, 'Missing': 4, 'Crown': 5, 'Endodontics': 6, 'Extraction': 8 };

  Object.entries(AppState.toothStates).forEach(([key, val]) => {
    const [t, s] = key.split('_');
    toothSurfaces.push({
      toothNumber: parseInt(t, 10),
      surface: parseInt(s, 10),
      conditionType: map[val.condition] || 0,
      status: val.status,
      estimatedPrice: val.price || 0
    });
  });

  Object.entries(AppState.wholeToothStates).forEach(([t, val]) => {
    toothSurfaces.push({
      toothNumber: parseInt(t, 10),
      surface: 0,
      conditionType: map[val.condition] || 0,
      status: val.status,
      estimatedPrice: val.price || 0
    });
  });

  const payload = {
    patientId: AppState.activePatient ? AppState.activePatient.id : 1,
    dentistId: 1,
    snapshotTitle: "Evolución y Presupuesto Odontológico",
    clinicalNotes: document.getElementById('doctor-notes-textarea')?.value || '',
    toothSurfaces: toothSurfaces
  };

  try {
    const res = await fetch(`/api/patients/${payload.patientId}/odontogram`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    if (res.ok) {
      showToastNotification("Odontograma y presupuesto guardados con éxito.", "success");
    }
  } catch (err) {}
}

// ==========================================
// 6. ASISTENTE IA CLÍNICO (CERO ALUCINACIONES)
// ==========================================
async function triggerClinicalAiModal() {
  triggerHaptic();
  const modal = document.getElementById('modal-ai-audit');
  const content = document.getElementById('ai-audit-content');
  if (!modal || !content) return;

  modal.classList.remove('hidden');
  content.innerHTML = `<div class="p-4 text-purple-600 animate-pulse">Auditando Folio ${AppState.activeFolio} en la base de datos clínica...</div>`;

  try {
    const res = await fetch('/api/ai/audit', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ folio: AppState.activeFolio, query: "Auditoría de seguridad médica" })
    });
    const data = await res.json();
    renderAiAuditResponse(data);
  } catch (err) {
    content.innerHTML = `<p class="text-rose-600 font-bold">Error en la auditoría del paciente.</p>`;
  }
}

function closeClinicalAiModal() {
  document.getElementById('modal-ai-audit')?.classList.add('hidden');
}

async function askCustomAiQuery() {
  const promptInput = document.getElementById('ai-prompt-input');
  if (!promptInput || !promptInput.value.trim()) return;

  const content = document.getElementById('ai-audit-content');
  content.innerHTML = `<div class="p-3 text-purple-600 animate-pulse">Analizando pregunta sobre ${AppState.activeFolio}...</div>`;

  try {
    const res = await fetch('/api/ai/audit', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ folio: AppState.activeFolio, query: promptInput.value.trim() })
    });
    const data = await res.json();
    renderAiAuditResponse(data);
    promptInput.value = '';
  } catch (err) {}
}

function renderAiAuditResponse(data) {
  const content = document.getElementById('ai-audit-content');
  if (!content) return;

  let alertBadge = data.hasCriticalAllergies ? `
    <div class="p-3 rounded-xl bg-rose-50 dark:bg-rose-950/60 border border-rose-300 dark:border-rose-800 text-rose-800 dark:text-rose-200">
      <h5 class="font-black flex items-center gap-1.5 text-xs">
        <span class="w-2 h-2 rounded-full bg-rose-600 animate-ping"></span>
        ALERGIAS REGISTRADAS EN EXPEDIENTE:
      </h5>
      <ul class="list-disc list-inside mt-1 font-bold">
        ${data.criticalAlerts.map(a => `<li>${a}</li>`).join('')}
      </ul>
    </div>
  ` : '';

  let warningsHtml = data.contraindicationsAndWarnings.length > 0 ? `
    <div class="p-3 rounded-xl bg-amber-50 dark:bg-amber-950/50 border border-amber-300 dark:border-amber-800 text-amber-900 dark:text-amber-200 text-xs space-y-1">
      <h5 class="font-black">CONTRAINDICACIONES Y REGLAS DE SEGURIDAD:</h5>
      ${data.contraindicationsAndWarnings.map(w => `<p>${w}</p>`).join('')}
    </div>
  ` : '';

  content.innerHTML = `
    <div class="space-y-3">
      ${alertBadge}
      ${warningsHtml}
      <div class="p-3 rounded-xl bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 whitespace-pre-line leading-relaxed">
        ${data.aiAnalysisMarkdown}
      </div>
    </div>
  `;
}

// ==========================================
// 7. PANEL ADMINISTRATIVO & WHATSAPP DIRECTO
// ==========================================
function loadAdminPanel() {
  const select = document.getElementById('wa-patient-select');
  if (select) {
    const validPatients = AppState.kanbanAppointments.filter(a => a.patientPhone);
    select.innerHTML = validPatients.map(a => `
      <option value="${a.patientPhone}" data-name="${a.patientName}" data-time="${new Date(a.startDateTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}" data-service="${a.serviceName}">
        ${a.patientName} (${a.patientPhone}) - ${a.serviceName}
      </option>
    `).join('');

    if (validPatients.length > 0) {
      populateWhatsAppForm(validPatients[0].patientPhone);
    }
  }

  // Inicializar selector de doctores para bloqueo de agenda
  const blockDoctorSelect = document.getElementById('block-dentist-select');
  if (blockDoctorSelect && AppState.dentists.length > 0) {
    blockDoctorSelect.innerHTML = AppState.dentists.map(d => `
      <option value="${d.id}">${d.fullName} (${d.specialty})</option>
    `).join('');
  }

  const blockDateInput = document.getElementById('block-date-input');
  if (blockDateInput && !blockDateInput.value) {
    blockDateInput.value = new Date().toISOString().split('T')[0];
    blockDateInput.onchange = () => loadDoctorScheduleExceptions();
  }

  loadDoctorScheduleExceptions();
}

async function loadDoctorScheduleExceptions() {
  const container = document.getElementById('active-exceptions-list');
  if (!container) return;

  const dateInput = document.getElementById('block-date-input');
  const targetDate = dateInput?.value || new Date().toISOString().split('T')[0];
  const doctorSelect = document.getElementById('block-dentist-select');
  const dentistId = doctorSelect?.value || 1;

  try {
    const res = await fetch(`/api/dentists/${dentistId}/exceptions?date=${targetDate}`);
    if (!res.ok) return;
    const exceptions = await res.json();

    if (exceptions.length === 0) {
      container.innerHTML = `
        <div class="p-3 rounded-xl bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-center text-slate-400 text-xs italic">
          No hay horarios bloqueados para esta fecha. Toda la agenda regular está abierta para citas.
        </div>
      `;
      return;
    }

    container.innerHTML = exceptions.map(ex => `
      <div class="p-3 rounded-xl bg-white dark:bg-slate-900 border border-rose-200 dark:border-rose-950 shadow-sm flex items-center justify-between gap-2">
        <div class="flex items-center gap-2.5">
          <div class="w-8 h-8 rounded-lg bg-rose-100 dark:bg-rose-950 text-rose-600 flex items-center justify-center font-bold text-xs">
            🔴
          </div>
          <div>
            <div class="flex items-center gap-1.5">
              <span class="font-extrabold text-slate-900 dark:text-white text-xs">${ex.formattedTimeRange}</span>
              <span class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-rose-100 text-rose-800 dark:bg-rose-900 dark:text-rose-200">Bloqueado</span>
            </div>
            <p class="text-[11px] text-slate-500 font-medium">${ex.dentistName} • ${ex.reason}</p>
          </div>
        </div>
        <button type="button" onclick="releaseScheduleException(${ex.id})" class="btn-smooth px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-xs flex items-center gap-1 shadow">
          <span>🟢 Liberar Horario</span>
        </button>
      </div>
    `).join('');
  } catch (err) {
    console.error("Error al cargar excepciones de agenda:", err);
  }
}

async function submitBlockSchedule(e) {
  e.preventDefault();
  triggerHaptic();

  const dentistId = parseInt(document.getElementById('block-dentist-select').value, 10);
  const date = document.getElementById('block-date-input').value;
  const startTime = document.getElementById('block-start-time').value;
  const endTime = document.getElementById('block-end-time').value;
  const reason = document.getElementById('block-reason-text').value.trim() || 'Bloqueo Médico';

  if (!date || !startTime || !endTime) {
    alert("Por favor completa la fecha y horas de inicio y fin.");
    return;
  }

  const payload = {
    dentistId: dentistId,
    startDateTime: `${date}T${startTime}:00`,
    endDateTime: `${date}T${endTime}:00`,
    reason: reason
  };

  try {
    const res = await fetch('/api/dentists/block-slot', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    const result = await res.json();
    if (res.ok && result.success) {
      showToastNotification(result.message || "Horario bloqueado con éxito.", "warning");
      loadDoctorScheduleExceptions();
    } else {
      alert(result.message || "No se pudo bloquear el horario.");
    }
  } catch (err) {
    alert("Error de conexión al bloquear horario.");
  }
}

async function releaseScheduleException(exceptionId) {
  triggerHaptic();
  try {
    const res = await fetch(`/api/dentists/exceptions/${exceptionId}`, {
      method: 'DELETE'
    });
    const result = await res.json();
    if (res.ok && result.success) {
      showToastNotification("Horario liberado. El cupo vuelve a estar disponible para agendar.", "success");
      loadDoctorScheduleExceptions();
    } else {
      alert(result.message || "No se pudo liberar el horario.");
    }
  } catch (err) {
    alert("Error al liberar horario.");
  }
}

function populateWhatsAppForm(phone) {
  const select = document.getElementById('wa-patient-select');
  const opt = select.selectedOptions[0];
  if (!opt) return;

  const name = opt.getAttribute('data-name');
  const time = opt.getAttribute('data-time');
  const service = opt.getAttribute('data-service');
  const templateType = document.getElementById('wa-template-select')?.value || 'confirm-15m';

  const msgBox = document.getElementById('wa-message-text');
  if (templateType === 'confirm-15m') {
    msgBox.value = `Hola ${name}, te recordamos tu cita de ${service} hoy a las ${time} en DentalCare. Por favor recuerda que debes confirmar tu asistencia a más tardar 15 minutos antes de la hora acordada, de lo contrario tu turno se reasignará. ¡Te esperamos!`;
  } else if (templateType === 'location') {
    msgBox.value = `Hola ${name}, te compartimos nuestra ubicación en Google Maps para tu cita: https://maps.google.com/?q=Clinica+Dental+Care. Contamos con estacionamiento en la entrada.`;
  } else if (templateType === 'postop') {
    msgBox.value = `Hola ${name}, esperamos que te sientas muy bien. Recuerda no comer alimentos duros ni calientes las próximas 24 horas y tomar tus medicamentos según tu receta médica disponible en tu expediente.`;
  }
}

function applyWhatsAppTemplate(val) {
  const select = document.getElementById('wa-patient-select');
  if (select) populateWhatsAppForm(select.value);
}

function launchWhatsAppDirect() {
  const select = document.getElementById('wa-patient-select');
  const phone = select ? select.value.replace(/[^0-9]/g, '') : '';
  const text = encodeURIComponent(document.getElementById('wa-message-text')?.value || '');
  if (!phone) {
    alert("Selecciona un paciente con teléfono válido.");
    return;
  }
  window.open(`https://wa.me/${phone}?text=${text}`, '_blank');
}

async function quickAdminBooking(e) {
  e.preventDefault();
  triggerHaptic();

  const name = document.getElementById('admin-quick-name').value.trim();
  const phone = document.getElementById('admin-quick-phone').value.trim();
  const serviceId = parseInt(document.getElementById('admin-quick-service').value, 10);
  const dateTime = document.getElementById('admin-quick-datetime').value;

  const parts = name.split(' ');
  const firstName = parts[0] || 'Paciente';
  const lastName = parts.slice(1).join(' ') || 'General';

  const payload = {
    serviceId: serviceId,
    startDateTime: new Date(dateTime).toISOString(),
    firstName: firstName,
    lastName: lastName,
    phoneNumber: phone
  };

  try {
    const res = await fetch('/api/appointments/book', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    const result = await res.json();
    if (res.ok && result.success) {
      showToastNotification(`Cita telefónica agendada para ${name}.`, "success");
      switchView('reception');
    } else {
      alert(result.errorMessage || "Error al agendar.");
    }
  } catch (err) {}
}

// ==========================================
// 8. PORTAL DEL PACIENTE (MIS RECETAS & TURNO)
// ==========================================
async function loadPatientPortalView(folio) {
  try {
    const res = await fetch(`/api/patients/by-folio/${folio}`);
    if (!res.ok) return;
    const patient = await res.json();

    document.getElementById('portal-patient-name').textContent = `Hola, ${patient.firstName}`;
    document.getElementById('portal-patient-folio').textContent = patient.folioNumber;

    // Recetas Médicas y Horarios de Tomas
    const prescContainer = document.getElementById('portal-prescriptions-container');
    if (prescContainer) {
      if (patient.prescriptions.length === 0) {
        prescContainer.innerHTML = `<p class="text-xs text-slate-400 italic">No tienes recetas médicas activas.</p>`;
      } else {
        prescContainer.innerHTML = patient.prescriptions.map(pr => `
          <div class="p-3.5 rounded-2xl bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 shadow-sm space-y-2">
            <div class="flex justify-between items-center text-xs font-bold">
              <span class="text-sky-600">${pr.prescriptionFolio}</span>
              <span class="text-slate-400">${new Date(pr.createdAt).toLocaleDateString()}</span>
            </div>
            <p class="text-xs font-bold text-slate-800 dark:text-slate-200">${pr.diagnosis}</p>
            <div class="space-y-1.5 pt-1">
              ${pr.items.map(i => `
                <div class="p-2 rounded-xl bg-slate-50 dark:bg-slate-900 border border-slate-100 dark:border-slate-800 flex items-center justify-between text-xs">
                  <div>
                    <span class="font-extrabold text-slate-900 dark:text-white">${i.medicineName}</span>
                    <span class="block text-[11px] text-slate-500">${i.instructions} • ${i.schedule}</span>
                  </div>
                  <button onclick="markDoseTaken(this)" class="px-2.5 py-1 rounded-lg bg-emerald-100 text-emerald-800 text-[10px] font-bold hover:bg-emerald-200">
                    ✓ Tomar
                  </button>
                </div>
              `).join('')}
            </div>
          </div>
        `).join('');
      }
    }

    // Próximas Citas
    const aptContainer = document.getElementById('portal-appointments-container');
    if (aptContainer) {
      aptContainer.innerHTML = patient.appointments.map(a => `
        <div class="p-3 rounded-xl bg-slate-50 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 flex items-center justify-between text-xs">
          <div>
            <span class="font-bold text-slate-900 dark:text-white">${a.serviceName}</span>
            <span class="block text-[10px] text-slate-400">${new Date(a.startDateTime).toLocaleDateString()} ${new Date(a.startDateTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
          </div>
          <span class="px-2 py-0.5 rounded-full text-[10px] font-extrabold bg-sky-100 text-sky-800">${a.status}</span>
        </div>
      `).join('');
    }
  } catch (err) {}
}

function markDoseTaken(btn) {
  triggerHaptic();
  btn.className = "px-2.5 py-1 rounded-lg bg-slate-200 text-slate-500 text-[10px] font-bold";
  btn.textContent = "✓ Tomada";
  btn.disabled = true;
  showToastNotification("Dosis registrada correctamente.", "success");
}
