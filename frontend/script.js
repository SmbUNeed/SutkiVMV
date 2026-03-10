/* ══════════════════════════════════════════════════════════
   script.js — главная страница v2
   ══════════════════════════════════════════════════════════ */
// API_BASE и утилиты авторизации приходят из auth.js

let apartments         = [];
let filteredApartments = [];
let currentApartment   = null;
let currentDate        = new Date();
let currentAptBookedDates = [];
let selectedCheckIn    = null;
let selectedCheckOut   = null;

// Выбранные удобства (Set имён)
let selectedAmenities = new Set();

// ── Хелпер URL для изображений ──────────────────────────
function imgUrl(path) {
    if (!path) return null;
    if (path.startsWith('http')) return path;
    return API_BASE + path;
}

// ── API ─────────────────────────────────────────────────
async function apiGetApartments() {
    try {
        const res = await fetch(`${API_URL}/apartments/detailed`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return await res.json();
    } catch (err) {
        console.error('Ошибка загрузки квартир:', err);
        return [];
    }
}

async function apiGetAmenities() {
    try {
        const res = await fetch(`${API_URL}/amenities`);
        return res.ok ? await res.json() : [];
    } catch { return []; }
}

async function apiBookAsUser(apartmentId, startDate, endDate) {
    const res = await authFetch('/auth/book', {
        method: 'POST',
        body: JSON.stringify({ apartmentId, startDate, endDate })
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Ошибка бронирования');
    return data;
}

// ── Grid ─────────────────────────────────────────────────
const grid = document.getElementById('apartmentsGrid');

function showSkeletons(count = 6) {
    grid.innerHTML = Array(count).fill(`
        <div class="apartment-card skeleton">
            <div class="card-image skeleton-box"></div>
            <div class="card-content">
                <div class="skeleton-line skeleton-box"></div>
                <div class="skeleton-line short skeleton-box"></div>
            </div>
        </div>`).join('');
}

function renderGrid(data) {
    grid.innerHTML = '';
    const countEl = document.getElementById('resultsCount');
    if (countEl) {
        const n = data.length;
        const label = n === 1 ? 'квартира' : n < 5 ? 'квартиры' : 'квартир';
        countEl.innerHTML = `<strong>${n}</strong> ${label}`;
    }

    if (!data.length) {
        grid.innerHTML = `
            <div class="no-results">
                <i class="fa-solid fa-magnifying-glass"></i>
                Квартир по вашим критериям не найдено.<br>Попробуйте изменить фильтры.
            </div>`;
        return;
    }
    data.forEach(apt => {
        const imgSrc  = imgUrl(apt.mainImage);
        const amenText = (apt.amenities && apt.amenities.length)
            ? apt.amenities.map(a => a.name).slice(0, 3).join(', ') + (apt.amenities.length > 3 ? '...' : '')
            : '—';

        grid.insertAdjacentHTML('beforeend', `
            <div class="apartment-card" onclick="openModal(${apt.id})">
                <div class="card-image">
                    ${imgSrc
                        ? `<img src="${imgSrc}" alt="${apt.title}" loading="lazy">`
                        : `<div class="no-photo-placeholder"><i class="fa-regular fa-image fa-2x"></i><span>Нет фото</span></div>`}
                    <div class="price-badge">от ${apt.pricePerNight.toLocaleString('ru-RU')} ₽</div>
                </div>
                <div class="card-content">
                    <h3>${apt.title}</h3>
                    <p class="specs">
                        <i class="fa-solid fa-user-group"></i> до ${apt.guests} гостей &nbsp;•&nbsp;
                        <i class="fa-solid fa-ruler-combined"></i> ${apt.area} м²
                    </p>
                    <p class="amenities-preview">${amenText}</p>
                    <div class="card-footer">
                        <span class="status available"><i class="fa-solid fa-circle" style="font-size:0.55rem;"></i> Свободна</span>
                        <button class="btn-primary">Посмотреть</button>
                    </div>
                </div>
            </div>`);
    });
}

// ── Фильтрация ───────────────────────────────────────────
function applyFilters() {
    const guests   = parseInt(document.getElementById('guests').value)   || 0;
    const priceMin = parseInt(document.getElementById('priceMin').value) || 0;
    const priceMax = parseInt(document.getElementById('priceMax').value) || Infinity;
    const sortVal  = document.getElementById('sortSelect').value;

    filteredApartments = apartments.filter(apt => {
        if (apt.guests < guests)             return false;
        if (apt.pricePerNight < priceMin)    return false;
        if (apt.pricePerNight > priceMax)    return false;
        // Мультивыбор удобств: квартира должна иметь ВСЕ выбранные удобства
        if (selectedAmenities.size > 0) {
            const aptAmenityNames = (apt.amenities || []).map(a => a.name);
            for (const name of selectedAmenities) {
                if (!aptAmenityNames.includes(name)) return false;
            }
        }
        return true;
    });

    if (sortVal === 'price_asc')  filteredApartments.sort((a, b) => a.pricePerNight - b.pricePerNight);
    if (sortVal === 'price_desc') filteredApartments.sort((a, b) => b.pricePerNight - a.pricePerNight);
    if (sortVal === 'area_desc')  filteredApartments.sort((a, b) => b.area - a.area);

    renderGrid(filteredApartments);
    updateFilterBadge();
}

function updateFilterBadge() {
    const guests   = parseInt(document.getElementById('guests').value)   || 0;
    const priceMin = parseInt(document.getElementById('priceMin').value) || 0;
    const priceMax = parseInt(document.getElementById('priceMax').value) || 0;

    let count = 0;
    if (guests > 0)   count++;
    if (priceMin > 0) count++;
    if (priceMax > 0) count++;
    count += selectedAmenities.size;

    const badge = document.getElementById('filterBadge');
    const btn   = document.getElementById('btnFilterOpen');
    if (count > 0) {
        badge.textContent = count;
        badge.classList.add('visible');
        btn.classList.add('has-filters');
    } else {
        badge.classList.remove('visible');
        btn.classList.remove('has-filters');
    }

    // Обновить счётчик удобств в панели
    const countEl = document.getElementById('amenitySelectedCount');
    if (countEl) {
        if (selectedAmenities.size > 0) {
            countEl.textContent = `(выбрано: ${selectedAmenities.size})`;
            countEl.style.display = 'inline';
        } else {
            countEl.style.display = 'none';
        }
    }
}

function resetFilters() {
    document.getElementById('guests').value   = '';
    document.getElementById('priceMin').value = '';
    document.getElementById('priceMax').value = '';
    selectedAmenities.clear();
    // Снять все галочки
    document.querySelectorAll('.amenity-check-item').forEach(el => {
        el.classList.remove('checked');
        el.querySelector('.amenity-checkmark').innerHTML = '';
    });
    applyFilters();
}

// ── Фильтр-панель ────────────────────────────────────────
function openFilterPanel() {
    document.getElementById('filterPanel').classList.add('open');
    document.getElementById('filterOverlay').classList.add('open');
    document.body.style.overflow = 'hidden';
}

function closeFilterPanel() {
    document.getElementById('filterPanel').classList.remove('open');
    document.getElementById('filterOverlay').classList.remove('open');
    document.body.style.overflow = '';
}

// ── Заполнение фильтра удобствами ────────────────────────
async function loadAmenityFilter() {
    const amenities = await apiGetAmenities();
    const list = document.getElementById('amenityCheckList');
    if (!amenities.length) {
        list.innerHTML = '<div style="color:var(--ink-3);font-size:0.85rem;padding:8px 0;">Удобства не найдены</div>';
        return;
    }
    list.innerHTML = amenities.map(a => `
        <label class="amenity-check-item" data-name="${a.name}" onclick="toggleAmenity(this)">
            <input type="checkbox" value="${a.name}">
            <span class="amenity-checkmark"></span>
            <i class="fa-solid ${a.icon || 'fa-check'} amenity-icon"></i>
            <span class="amenity-label">${a.name}</span>
        </label>`).join('');
}

function toggleAmenity(el) {
    const name = el.getAttribute('data-name');
    const checked = el.classList.toggle('checked');
    const mark = el.querySelector('.amenity-checkmark');
    if (checked) {
        selectedAmenities.add(name);
        mark.innerHTML = '<i class="fa-solid fa-check" style="font-size:0.6rem;"></i>';
    } else {
        selectedAmenities.delete(name);
        mark.innerHTML = '';
    }
    updateFilterBadge();
}

// ── Защита числовых полей от отрицательных значений ───────
function guardNumberInputs() {
    document.querySelectorAll('input[type="number"]').forEach(input => {
        const minVal = parseFloat(input.min);
        const floor  = isNaN(minVal) ? 0 : minVal;
        input.addEventListener('input', () => {
            const val = parseFloat(input.value);
            if (!isNaN(val) && val < floor) {
                input.value = floor;
            }
        });
        input.addEventListener('change', () => {
            const val = parseFloat(input.value);
            if (!isNaN(val) && val < floor) {
                input.value = floor;
            }
        });
    });
}

// ── Modal ────────────────────────────────────────────────
function openModal(apartmentId) {
    const apt = apartments.find(a => a.id === apartmentId);
    if (!apt) return;
    currentApartment = apt;

    document.getElementById('modalTitle').textContent = apt.title;
    document.getElementById('modalSpecs').innerHTML = `
        <span><i class="fa-solid fa-user-group"></i> до ${apt.guests} гостей</span>
        <span><i class="fa-solid fa-ruler-combined"></i> ${apt.area} м²</span>
        <span><i class="fa-solid fa-door-open"></i> ${apt.roomsCount} комн.</span>`;
    document.getElementById('modalDesc').textContent = apt.description;
    document.getElementById('modalPrice').innerHTML =
        `${apt.pricePerNight.toLocaleString('ru-RU')} ₽ <span>/ ночь</span>`;

    // Галерея
    const mainImg = document.getElementById('modalMainImg');
    const thumbs  = document.getElementById('modalThumbs');
    const firstSrc = imgUrl(apt.mainImage) || 'https://placehold.co/600x340?text=Нет+фото';
    mainImg.src = firstSrc;
    mainImg.style.display = 'block';
    thumbs.innerHTML = '';

    if (apt.gallery && apt.gallery.length > 1) {
        apt.gallery.forEach(g => {
            const img   = document.createElement('img');
            img.src     = imgUrl(g.imagePath);
            img.loading = 'lazy';
            if (g.isMain) img.classList.add('active');
            img.onclick = () => {
                mainImg.src = img.src;
                thumbs.querySelectorAll('img').forEach(t => t.classList.remove('active'));
                img.classList.add('active');
            };
            thumbs.appendChild(img);
        });
    }

    // Удобства
    const amenEl = document.getElementById('modalAmenities');
    amenEl.innerHTML = (apt.amenities && apt.amenities.length)
        ? apt.amenities.map(a =>
            `<span class="amenity-tag"><i class="fa-solid ${a.icon}"></i>${a.name}</span>`).join('')
        : '';

    // Сброс дат
    selectedCheckIn = selectedCheckOut = null;
    currentAptBookedDates = getBookedDates(apt.bookings || []);
    currentDate = new Date();
    updateBookingSummary();
    renderCalendar();
    renderBookingWidget();

    document.getElementById('apartmentModal').classList.add('open');
    document.body.style.overflow = 'hidden';
}

function closeModal() {
    document.getElementById('apartmentModal').classList.remove('open');
    document.body.style.overflow = '';
    currentApartment = null;
}

document.getElementById('apartmentModal').addEventListener('click', e => {
    if (e.target === e.currentTarget) closeModal();
});

// ── Виджет бронирования ──────────────────────────────────
function renderBookingWidget() {
    const el = document.getElementById('bookingWidget');
    if (isLoggedIn()) {
        el.innerHTML = `
            <button class="btn-book" id="btnBook" onclick="handleBook()">
                <i class="fa-solid fa-calendar-check"></i> Забронировать
            </button>`;
    } else {
        el.innerHTML = `
            <div class="auth-gate">
                <p>Чтобы забронировать квартиру,<br>войдите в аккаунт или зарегистрируйтесь</p>
                <div class="auth-gate-btns">
                    <a href="login.html" class="btn-auth btn-auth-primary">
                        <i class="fa-solid fa-arrow-right-to-bracket"></i> Войти
                    </a>
                    <a href="register.html" class="btn-auth btn-auth-ghost">
                        Зарегистрироваться
                    </a>
                </div>
            </div>`;
    }
}

// ── Бронирование ─────────────────────────────────────────
async function handleBook() {
    if (!selectedCheckIn || !selectedCheckOut) {
        alert('Пожалуйста, выберите даты заезда и выезда.');
        return;
    }
    const btn = document.getElementById('btnBook');
    btn.disabled = true;
    btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Оформляем...';

    try {
        await apiBookAsUser(currentApartment.id, selectedCheckIn, selectedCheckOut);
        alert('Бронирование успешно оформлено! Ожидайте подтверждения.');
        closeModal();
        apartments = await apiGetApartments();
        filteredApartments = [...apartments];
        applyFilters();
    } catch (err) {
        alert(`Ошибка: ${err.message}`);
        btn.disabled = false;
        btn.innerHTML = '<i class="fa-solid fa-calendar-check"></i> Забронировать';
    }
}

// ── Календарь ────────────────────────────────────────────
const MONTH_NAMES = [
    'Январь','Февраль','Март','Апрель','Май','Июнь',
    'Июль','Август','Сентябрь','Октябрь','Ноябрь','Декабрь'
];

function getBookedDates(bookings) {
    const dates = [];
    for (const b of bookings) {
        if (b.status === 'Cancelled') continue;
        const start = new Date(b.startDate);
        const end   = new Date(b.endDate);
        for (let d = new Date(start); d < end; d.setDate(d.getDate() + 1))
            dates.push(d.toISOString().slice(0, 10));
    }
    return dates;
}

function toDateStr(d) { return d.toISOString().slice(0, 10); }

function renderCalendar() {
    const year     = currentDate.getFullYear();
    const month    = currentDate.getMonth();
    const firstDay = new Date(year, month, 1).getDay();
    const lastDay  = new Date(year, month + 1, 0).getDate();
    const startDay = firstDay === 0 ? 6 : firstDay - 1;
    const todayStr = toDateStr(new Date());

    let html = `
        <div class="cal-header">
            <button class="cal-nav" onclick="changeMonth(-1)"><i class="fa-solid fa-chevron-left"></i></button>
            <span class="cal-month">${MONTH_NAMES[month]} ${year}</span>
            <button class="cal-nav" onclick="changeMonth(1)"><i class="fa-solid fa-chevron-right"></i></button>
        </div>
        <div class="cal-grid">
            ${['Пн','Вт','Ср','Чт','Пт','Сб','Вс'].map(d =>
                `<div class="cal-dow">${d}</div>`).join('')}
            ${"<div class='cal-day empty'></div>".repeat(startDay)}`;

    for (let i = 1; i <= lastDay; i++) {
        const ds       = `${year}-${String(month+1).padStart(2,'0')}-${String(i).padStart(2,'0')}`;
        const isPast   = ds < todayStr;
        const isBooked = currentAptBookedDates.includes(ds);
        const isStart  = ds === selectedCheckIn;
        const isEnd    = ds === selectedCheckOut;
        const inRange  = selectedCheckIn && selectedCheckOut && ds > selectedCheckIn && ds < selectedCheckOut;

        let cls = 'cal-day';
        if (isPast)           cls += ' past';
        if (isBooked)         cls += ' booked';
        if (isStart)          cls += ' check-in';
        else if (isEnd)       cls += ' check-out';
        else if (inRange)     cls += ' in-range';

        const click = (!isPast && !isBooked) ? `onclick="selectDate('${ds}')"` : '';
        html += `<div class="${cls}" ${click}>${i}</div>`;
    }
    html += '</div>';
    document.getElementById('bookingCalendar').innerHTML = html;
}

function changeMonth(step) {
    currentDate.setMonth(currentDate.getMonth() + step);
    renderCalendar();
}

function selectDate(ds) {
    if (!selectedCheckIn || (selectedCheckIn && selectedCheckOut)) {
        selectedCheckIn  = ds;
        selectedCheckOut = null;
    } else if (ds <= selectedCheckIn) {
        selectedCheckIn  = ds;
    } else {
        let conflict = false;
        for (let d = new Date(selectedCheckIn); d < new Date(ds); d.setDate(d.getDate()+1)) {
            if (currentAptBookedDates.includes(toDateStr(d))) { conflict = true; break; }
        }
        if (conflict) {
            alert('В выбранном диапазоне есть занятые даты. Выберите другой период.');
            selectedCheckIn  = ds;
            selectedCheckOut = null;
        } else {
            selectedCheckOut = ds;
        }
    }
    renderCalendar();
    updateBookingSummary();
}

function updateBookingSummary() {
    const el = document.getElementById('bookingSummary');
    if (!el) return;
    if (selectedCheckIn && selectedCheckOut && currentApartment) {
        const nights = (new Date(selectedCheckOut) - new Date(selectedCheckIn)) / 86_400_000;
        const total  = nights * currentApartment.pricePerNight;
        const label  = nights === 1 ? 'ночь' : nights < 5 ? 'ночи' : 'ночей';
        el.innerHTML = `
            <div class="row"><span>${nights} ${label}</span><span>${currentApartment.pricePerNight.toLocaleString('ru-RU')} ₽/ночь</span></div>
            <div class="row total"><span>Итого</span><strong>${total.toLocaleString('ru-RU')} ₽</strong></div>`;
        el.style.display = 'block';
    } else {
        el.style.display = 'none';
    }
}

// ── Инициализация ────────────────────────────────────────
async function init() {
    showSkeletons();
    await loadAmenityFilter();
    apartments = await apiGetApartments();
    filteredApartments = [...apartments];
    renderGrid(filteredApartments);
    guardNumberInputs();

    // Input listeners for live filter
    ['guests', 'priceMin', 'priceMax'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.addEventListener('input', applyFilters);
    });
}

init();
