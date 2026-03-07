/* ══════════════════════════════════════════════════════════
   script.js — главная страница
   ══════════════════════════════════════════════════════════ */
// API_BASE и утилиты авторизации приходят из auth.js

let apartments        = [];
let filteredApartments = [];
let currentApartment  = null;
let currentDate       = new Date();
let currentAptBookedDates = [];
let selectedCheckIn   = null;
let selectedCheckOut  = null;

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
    if (!data.length) {
        grid.innerHTML = '<p class="no-results">Квартир по вашим критериям не найдено. Попробуйте изменить фильтры.</p>';
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
                        <span class="status available">Свободна</span>
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
    const amenity  = document.getElementById('amenityFilter').value;
    const sortVal  = document.getElementById('sortSelect').value;

    filteredApartments = apartments.filter(apt =>
        apt.guests        >= guests   &&
        apt.pricePerNight >= priceMin &&
        apt.pricePerNight <= priceMax &&
        (amenity === '' || (apt.amenities && apt.amenities.some(a => a.name === amenity)))
    );

    if (sortVal === 'price_asc')  filteredApartments.sort((a, b) => a.pricePerNight - b.pricePerNight);
    if (sortVal === 'price_desc') filteredApartments.sort((a, b) => b.pricePerNight - a.pricePerNight);
    if (sortVal === 'area_desc')  filteredApartments.sort((a, b) => b.area - a.area);

    renderGrid(filteredApartments);
}

['guests', 'priceMin', 'priceMax'].forEach(id =>
    document.getElementById(id).addEventListener('input', applyFilters));
['amenityFilter', 'sortSelect'].forEach(id =>
    document.getElementById(id).addEventListener('change', applyFilters));

// ── Заполнение фильтра удобствами ────────────────────────

async function loadAmenityFilter() {
    const amenities = await apiGetAmenities();
    const sel = document.getElementById('amenityFilter');
    amenities.forEach(a => {
        const opt = document.createElement('option');
        opt.value = a.name;
        opt.textContent = a.name;
        sel.appendChild(opt);
    });
}

// ── Modal ────────────────────────────────────────────────

function openModal(apartmentId) {
    const apt = apartments.find(a => a.id === apartmentId);
    if (!apt) return;
    currentApartment = apt;

    document.getElementById('modalTitle').textContent = apt.title;
    document.getElementById('modalSpecs').textContent =
        `Вместимость: ${apt.guests} гостей  •  Площадь: ${apt.area} м²  •  ${apt.roomsCount} комн.`;
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
            const img = document.createElement('img');
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

// ── Виджет бронирования (auth-aware) ─────────────────────

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
                <div style="display:flex;gap:10px;flex-wrap:wrap;justify-content:center;margin-top:4px;">
                    <a href="login.html" class="btn-auth btn-auth-primary">
                        <i class="fa-solid fa-arrow-right-to-bracket"></i> Войти
                    </a>
                    <a href="register.html" class="btn-auth btn-auth-ghost" style="color:var(--ink-2);border:1.5px solid var(--border-dk);">
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
        if (isPast)             cls += ' past';
        if (isBooked)           cls += ' booked';
        if (isStart)            cls += ' check-in';
        else if (isEnd)         cls += ' check-out';
        else if (inRange)       cls += ' in-range';

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
}

init();
