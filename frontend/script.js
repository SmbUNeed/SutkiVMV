/* ══════════════════════════════════════════════════════════
   script.js — главная страница v2
   ══════════════════════════════════════════════════════════ */
// API_BASE и утилиты авторизации приходят из auth.js

let apartments = [];
let filteredApartments = [];
let currentApartment = null;
let currentDate = new Date();
let currentAptBookedDates = [];
let selectedCheckIn = null;
let selectedCheckOut = null;
let guests = 1;

// Выбранные удобства (Set имён)
let selectedAmenities = new Set();

let siteSettings = { minBookingDaysAhead: 0, maxBookingRangeDays: 365 };

async function loadSiteSettings() {
    const res = await fetch('/api/settings');
    siteSettings = await res.json();
}

// ── Хелпер URL для изображений ──────────────────────────
function imgUrl(path) {
    if (!path) return null;
    if (path.startsWith('https')) return path;
    return API_BASE + path;
}

// ── Мобильное меню (шапка) ───────────────────────────────
function toggleMobileMenu() {
    const actions = document.getElementById('headerActions');
    if (actions.classList.contains('open')) closeMobileMenu();
    else openMobileMenu();
}

function openMobileMenu() {
    document.getElementById('headerActions').classList.add('open');
    document.getElementById('mobileMenuOverlay').classList.add('open');
    document.body.style.overflow = 'hidden';
}

function closeMobileMenu() {
    document.getElementById('headerActions').classList.remove('open');
    document.getElementById('mobileMenuOverlay').classList.remove('open');
    document.body.style.overflow = '';
}

// ── API ─────────────────────────────────────────────────
async function apiGetApartments() {
    try {
        const res = await fetch(`${API_URL}/apartments/summary`);
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

async function apiBookAsUser(apartmentId, startDate, endDate, guests) {
    const res = await authFetch('/auth/book', {
        method: 'POST',
        body: JSON.stringify({ apartmentId, startDate, endDate, guests })
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Ошибка бронирования');
    return data;
}

const amenName = a => (typeof a === 'string' ? a : a?.name);

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
        const label = n === 1 ? 'квартира'
            : n < 5 ? 'квартиры'
            : n <= 20 ? 'квартир'
            : n%10 === 1 ? 'квартира'
            : n%10 < 5 ? "квартиры"
            : "квартир";
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
    const guestsText = n => {
        const m10 = n % 10, m100 = n % 100;
        return `до ${n} ${m10 === 1 && m100 !== 11 ? 'гостя' : 'гостей'}`;
    };

    data.forEach(apt => {
        const imgSrc = imgUrl(apt.mainImage);
        const amenNames = apt.amenities || [];
        const amenText = amenNames.length
            ? amenNames.slice(0, 3).join(', ') + (amenNames.length > 3 ? '...' : '')
            : '—';

        grid.insertAdjacentHTML('beforeend', `
            <div class="apartment-card" onclick="openModal(${apt.id})">
                <div class="card-image">
                    ${imgSrc
                ? `<img src="${imgSrc}" alt="${apt.title}" loading="lazy">`
                : `<div class="no-photo-placeholder"><i class="fa-regular fa-image fa-2x"></i><span>Нет фото</span></div>`}
                    <div class="price-badge">
                        <small>от</small>
                        <strong>${apt.pricePerNight.toLocaleString('ru-RU')} ₽</strong>
                        <small>/ ночь</small>
                    </div>
                </div>
                <div class="card-content">
                    <h3 title="${apt.title}">${apt.title}</h3>
                    <p class="specs">
                        <i class="fa-solid fa-user-group"></i> ${guestsText(apt.guests)} &nbsp;•&nbsp;
                        <i class="fa-solid fa-ruler-combined"></i> ${apt.area} м2
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
    const guests = parseInt(document.getElementById('guests').value) || 0;
    const priceMin = parseInt(document.getElementById('priceMin').value) || 0;
    const priceMax = parseInt(document.getElementById('priceMax').value) || Infinity;
    const sortVal = document.getElementById('sortSelect').value;

    filteredApartments = apartments.filter(apt => {
        if (apt.guests < guests) return false;
        if (apt.pricePerNight < priceMin) return false;
        if (apt.pricePerNight > priceMax) return false;
        // Мультивыбор удобств: квартира должна иметь ВСЕ выбранные удобства
        if (selectedAmenities.size > 0) {
            const aptAmenityNames = (apt.amenities || []).map(amenName);
            for (const name of selectedAmenities) {
                if (!aptAmenityNames.includes(name)) return false;
            }
        }
        return true;
    });

    if (sortVal === 'price_asc') filteredApartments.sort((a, b) => a.pricePerNight - b.pricePerNight);
    if (sortVal === 'price_desc') filteredApartments.sort((a, b) => b.pricePerNight - a.pricePerNight);
    if (sortVal === 'area_desc') filteredApartments.sort((a, b) => b.area - a.area);

    renderGrid(filteredApartments);
    updateFilterBadge();
}

function updateFilterBadge() {
    const guests = parseInt(document.getElementById('guests').value) || 0;
    const priceMin = parseInt(document.getElementById('priceMin').value) || 0;
    const priceMax = parseInt(document.getElementById('priceMax').value) || 0;

    let count = 0;
    if (guests > 0) count++;
    if (priceMin > 0) count++;
    if (priceMax > 0) count++;
    count += selectedAmenities.size;

    const badge = document.getElementById('filterBadge');
    const btn = document.getElementById('btnFilterOpen');
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
    document.getElementById('guests').value = '';
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

// Закрыть меню, если экран расширили до десктопного
window.addEventListener('resize', () => {
    if (window.innerWidth > 680) closeMobileMenu();
});

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
    const allAmenities = await apiGetAmenities();
    const list = document.getElementById('amenityCheckList');

    // Имена удобств, которые есть хотя бы у одной квартиры
    const usedNames = new Set(
        apartments.flatMap(a => (a.amenities || []).map(amenName))
    );
    const amenities = allAmenities.filter(a => usedNames.has(a.name));

    if (!amenities.length) {
        list.innerHTML = '<div style="color:var(--ink-3);font-size:0.85rem;padding:8px 0;">Удобства не найдены</div>';
        return;
    }
    list.innerHTML = amenities.map(a => `
        <label class="amenity-check-item" data-name="${a.name}" onclick="toggleAmenity(event, this)">
            <input type="checkbox" value="${a.name}">
            <span class="amenity-checkmark"></span>
            <i class="fa-solid ${a.icon || 'fa-check'} amenity-icon"></i>
            <span class="amenity-label">${a.name}</span>
        </label>`).join('');
}

function toggleAmenity(e, el) {
    e.preventDefault();
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
        const floor = isNaN(minVal) ? 0 : minVal;
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

function stepNum(id, delta) {
    const el  = document.getElementById(id);
    const val = parseInt(el.value) || 0;
    const min = parseInt(el.min) || 0;
    const max = parseInt(el.max) || Infinity;
    el.value  = Math.max(min, Math.min(max, val + delta)) || '';

    if (id === 'bookGuests') {
        updateBookingSummary();
    } else if (typeof applyFilters === 'function') {
        applyFilters();
    }
}

// ── Modal ────────────────────────────────────────────────
function showModalSkeleton() {
    document.getElementById('modalTitle').innerHTML = '<span class="skeleton skeleton-box skeleton-line" style="width:70%;height:24px;"></span>';
    document.getElementById('modalSpecs').innerHTML = '';
    document.getElementById('modalDesc').innerHTML = '<span class="skeleton skeleton-box skeleton-line"></span><span class="skeleton skeleton-box skeleton-line short"></span>';
    document.getElementById('modalMainImg').style.display = 'none';
    document.getElementById('modalThumbs').innerHTML = '';
    document.getElementById('modalPrice').innerHTML = '';
    document.getElementById('bookingCalendar').innerHTML = '<div class="skeleton skeleton-box" style="height:260px;border-radius:var(--r);"></div>';
}
function hideModalSkeleton() {
    document.getElementById('modalMainImg').style.display = 'block';
}
async function openModal(apartmentId, clearParams = false) {
    guests = 1;

    if (clearParams) history.replaceState(null, '', window.location.pathname);

    document.getElementById('apartmentModal').classList.add('open');
    document.body.style.overflow = 'hidden';
    showModalSkeleton();

    let apt;
    try {
        const res = await fetch(`${API_URL}/apartments/${apartmentId}`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        apt = await res.json();
    } catch (err) {
        console.error('Ошибка загрузки квартиры:', err);
        closeModal();
        alert('Не удалось загрузить квартиру. Попробуйте ещё раз.');
        return;
    }

    currentApartment = apt;
    hideModalSkeleton();

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
    const thumbs = document.getElementById('modalThumbs');
    const firstSrc = imgUrl(apt.mainImage) || 'https://placehold.co/600x340?text=Нет+фото';
    mainImg.src = firstSrc;
    mainImg.style.display = 'block';
    thumbs.innerHTML = '';

    const galleryUrls = (apt.gallery && apt.gallery.length)
        ? apt.gallery.map(g => imgUrl(g.imagePath))
        : [firstSrc];

    mainImg.onclick = () => {
        const currentIdx = galleryUrls.indexOf(mainImg.src) ?? 0;
        openLightbox(galleryUrls, currentIdx < 0 ? 0 : currentIdx);
    };

    if (apt.gallery && apt.gallery.length > 1) {
        apt.gallery.forEach(g => {
            const img = document.createElement('img');
            img.src = imgUrl(g.imagePath);
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
    currentAptBookedDates = getBookedDates(apt.busyPeriods || []);
    currentDate = new Date();
    updateBookingSummary();
    renderCalendar();
    renderBookingWidget();

    // Правила
    const rulesEl = document.getElementById('modalRules');
    rulesEl.innerHTML = (apt.rules && apt.rules.length)
        ? apt.rules.map(r => `
            <div class="modal-rule">
                <i class="fa-solid ${r.icon}"></i>
                <span>${r.text}</span>
            </div>`).join('')
        : '<p style="color:var(--ink-3);">Правила не указаны</p>';

    // Рядом
    const nearbyEl = document.getElementById('modalNearby');
    nearbyEl.innerHTML = (apt.nearby && apt.nearby.length)
        ? apt.nearby.map(n => {
            const inner = `
                <i class="fa-solid ${n.icon}"></i>
                <span>${n.name}</span>
                <span class="modal-nearby-dist">${n.distance}</span>
                ${n.wikiPlaceId ? '<i class="fa-solid fa-arrow-up-right-from-square modal-nearby-arrow"></i>' : ''}`;

            return n.wikiPlaceId
                ? `<a class="modal-nearby linked" href="wiki.html?place=${n.wikiPlaceId}">${inner}</a>`
                : `<div class="modal-nearby">${inner}</div>`;
        }).join('')
        : '<p style="color:var(--ink-3);">Не указано</p>';

    // Сброс вкладки на Описание
    switchTab('desc');
    document.querySelectorAll('.modal-tab').forEach(el => el.classList.remove('active'));
    document.querySelector('.modal-tab').classList.add('active');
}

function changeGuests(delta) {
    guests = Math.max(guests + delta, 0);
    const el = document.getElementById('guestCount');
    if (el) el.textContent = guests;
    updateBookingSummary();
}

function shareApartment() {
    if (!currentApartment) return;

    const url = `${location.origin}/?apt=${currentApartment.id}`;
    const title = currentApartment.title;
    const text = `${currentApartment.title} — ${currentApartment.pricePerNight.toLocaleString('ru-RU')} ₽/ночь`;

    if (navigator.share) {
        // Мобилка — нативный шаринг
        navigator.share({ title, text, url }).catch(() => { });
    } else {
        // Десктоп — копируем ссылку
        navigator.clipboard.writeText(url).then(() => {
            showToast('Ссылка скопирована');
        });
    }
}

function showToast(msg) {
    // Если у вас уже есть тост — используйте его
    // Иначе временный алерт
    const toast = document.createElement('div');
    toast.textContent = msg;
    toast.style.cssText = `
        position:fixed;bottom:28px;left:50%;transform:translateX(-50%);
        background:var(--ink);color:#fff;padding:10px 20px;
        border-radius:var(--r-full);font-size:.875rem;
        z-index:9999;pointer-events:none;`;
    document.body.appendChild(toast);
    setTimeout(() => toast.remove(), 2500);
}

function closeModal() {
    document.getElementById('apartmentModal').classList.remove('open');
    document.body.style.overflow = '';
    currentApartment = null;
}

document.getElementById('apartmentModal').addEventListener('click', e => {
    if (e.target === e.currentTarget) closeModal();
});

function changeGuests(delta) {
    guests = Math.max(guests + delta, 1); // было 0
    const el = document.getElementById('guestCount');
    if (el) el.textContent = guests;
    updateBookingSummary();
}

// ── Виджет бронирования ──────────────────────────────────
function renderBookingWidget() {
    const el = document.getElementById('bookingWidget');
    if (isLoggedIn()) {
        el.innerHTML = `
            <div class="guests-row">
                <div class="num-input">
                    <button type="button" onclick="stepNum('bookGuests',-1)">−</button>
                    <input type="number" id="bookGuests" min="1" max="20" value="1" oninput="updateBookingSummary()">
                    <button type="button" onclick="stepNum('bookGuests',1)">+</button>
                </div>
                <span style="color:var(--ink-3);font-size:0.85rem;">    Гостей</span>
            </div>
            <button class="btn-book" id="btnBook" onclick="handleBookFree()">
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
// Залогинен, бесплатно — переименовал handleBook -> handleBookFree, логика без изменений
async function handleBookFree() {
    if (!selectedCheckIn || !selectedCheckOut) {
        alert('Пожалуйста, выберите даты заезда и выезда.');
        return;
    }
    const guestsInput = document.getElementById('bookGuests');
    const adults = Math.max(1, parseInt(guestsInput?.value, 10) || 1);

    const btn = document.getElementById('btnBook');
    btn.disabled = true;
    btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Оформляем...';

    try {
        await apiBookAsUser(currentApartment.id, selectedCheckIn, selectedCheckOut, adults);
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

async function handlePayNow() {
    if (!selectedCheckIn || !selectedCheckOut) {
        alert('Пожалуйста, выберите даты заезда и выезда.');
        return;
    }
    const safeGuests = Math.max(guests, 1);
    const btn = document.getElementById('btnPayNow');
    btn.disabled = true;
    btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Оформляем...';

    try {
        const booking = await apiBookAsUser(currentApartment.id, selectedCheckIn, selectedCheckOut, safeGuests);
        const payment = await apiPayForBooking(booking.id);
        window.location.href = payment.url;
    } catch (err) {
        alert(`Ошибка: ${err.message}`);
        btn.disabled = false;
        btn.innerHTML = '<i class="fa-solid fa-credit-card"></i> Оплатить сразу';
    }
}

// Гость
async function handleGuestPay() {
    if (!selectedCheckIn || !selectedCheckOut) {
        alert('Пожалуйста, выберите даты заезда и выезда.');
        return;
    }

    const fullName = document.getElementById('guestName').value.trim();
    const phoneNumber = document.getElementById('guestPhone').value.trim();

    if (!fullName || !phoneNumber) {
        alert('Укажите имя и телефон.');
        return;
    }

    const btn = document.getElementById('btnGuestPay');
    btn.disabled = true;
    btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Оформляем...';

    try {
        const payment = await apiBookGuestAndPay(
            currentApartment.id, selectedCheckIn, selectedCheckOut, fullName, phoneNumber
        );
        window.location.href = payment.url;
    } catch (err) {
        alert(`Ошибка: ${err.message}`);
        btn.disabled = false;
        btn.innerHTML = '<i class="fa-solid fa-credit-card"></i> Оплатить и забронировать';
    }
}

async function apiPayForBooking(bookingId) {
    const res = await authFetch('/payments/create', {
        method: 'POST',
        body: JSON.stringify({ bookingId })
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Ошибка оплаты');
    return data;
}

async function apiBookGuestAndPay(apartmentId, startDate, endDate, guests, fullName, phoneNumber) {
    // Гостевой эндпоинт анонимный ([AllowAnonymous]) — authFetch тут не обязателен,
    // но можно использовать обычный fetch напрямую, раз токена всё равно нет
    const res = await fetch(`${API_URL}/payments/book-guest`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ apartmentId, startDate, endDate, guests, fullName, phoneNumber })
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.message || 'Ошибка бронирования');
    return data;
}

// ── Календарь ────────────────────────────────────────────
const MONTH_NAMES = [
    'Январь', 'Февраль', 'Март', 'Апрель', 'Май', 'Июнь',
    'Июль', 'Август', 'Сентябрь', 'Октябрь', 'Ноябрь', 'Декабрь'
];

function getBookedDates(busyPeriods) {
    const dates = [];
    for (const p of busyPeriods) {
        const start = new Date(p.startDate);
        const end = new Date(p.endDate);
        for (let d = new Date(start); d < end; d.setDate(d.getDate() + 1))
            dates.push(d.toISOString().slice(0, 10));
    }
    return dates;
}

function toDateStr(d) { return d.toISOString().slice(0, 10); }

function renderCalendar() {
    const year = currentDate.getFullYear();
    const month = currentDate.getMonth();
    const firstDay = new Date(year, month, 1).getDay();
    const lastDay = new Date(year, month + 1, 0).getDate();
    const startDay = firstDay === 0 ? 6 : firstDay - 1;

    const minDate = new Date();
    minDate.setDate(minDate.getDate() + siteSettings.minBookingDaysAhead);
    const minDateStr = toDateStr(minDate);

    const maxDate = new Date();
    maxDate.setDate(maxDate.getDate() + siteSettings.maxBookingRangeDays);
    const maxDateStr = toDateStr(maxDate);

    let html = `
        <div class="cal-header">
            <button class="cal-nav" onclick="changeMonth(-1)"><i class="fa-solid fa-chevron-left"></i></button>
            <span class="cal-month">${MONTH_NAMES[month]} ${year}</span>
            <button class="cal-nav" onclick="changeMonth(1)"><i class="fa-solid fa-chevron-right"></i></button>
        </div>
        <div class="cal-grid">
            ${['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс'].map(d =>
        `<div class="cal-dow">${d}</div>`).join('')}
            ${"<div class='cal-day empty'></div>".repeat(startDay)}`;

    const firstBooked = selectedCheckIn && !selectedCheckOut
        ? getFirstBookedAfter(selectedCheckIn)
        : null;

    for (let i = 1; i <= lastDay; i++) {
        const ds = `${year}-${String(month + 1).padStart(2, '0')}-${String(i).padStart(2, '0')}`;
        const isPast = ds < minDateStr || ds > maxDateStr;
        const isBooked = currentAptBookedDates.includes(ds);
        const isStart = ds === selectedCheckIn;
        const isEnd = ds === selectedCheckOut;
        const inRange = selectedCheckIn && selectedCheckOut && ds > selectedCheckIn && ds < selectedCheckOut;


        let cls = 'cal-day';
        if (isPast) cls += ' past';
        const isCheckoutOnly = !isPast && ds === firstBooked;
        if (isBooked && !isCheckoutOnly) cls += ' booked';
        if (isCheckoutOnly) cls += ' checkout-only';
        if (isStart) cls += ' check-in';
        else if (isEnd) cls += ' check-out';
        else if (inRange) cls += ' in-range';

        const canClick = !isPast && (!isBooked || isCheckoutOnly);
        const click = canClick ? `onclick="selectDate('${ds}')"` : '';
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
        selectedCheckIn = ds;
        selectedCheckOut = null;
    } else if (ds <= selectedCheckIn) {
        selectedCheckIn = ds;
    } else {
        let conflict = false;
        for (let d = new Date(selectedCheckIn); d < new Date(ds); d.setDate(d.getDate() + 1)) {
            if (currentAptBookedDates.includes(toDateStr(d))) { conflict = true; break; }
        }
        if (conflict) {
            alert('В выбранном диапазоне есть занятые даты. Выберите другой период.');
            selectedCheckIn = ds;
            selectedCheckOut = null;
        } else {
            selectedCheckOut = ds;
        }
    }
    renderCalendar();
    updateBookingSummary();
}

function getFirstBookedAfter(fromDs) {
    // сортируем занятые даты и берём первую после fromDs
    return currentAptBookedDates
        .filter(d => d > fromDs)
        .sort()[0] || null;
}

function updateBookingSummary() {
    const el = document.getElementById('bookingSummary');
    if (!el) return;
    if (selectedCheckIn && selectedCheckOut && currentApartment) {
        const nights = (new Date(selectedCheckOut) - new Date(selectedCheckIn)) / 86_400_000;
        let total = nights * currentApartment.pricePerNight;

        const guestsInput = document.getElementById('bookGuests');
        const currentGuests = Math.max(1, parseInt(guestsInput?.value, 10) || 1);
        const extraGuests = Math.max(0, currentGuests - currentApartment.guests);
        const overp = currentApartment.guestOverpayment > 0 ? currentApartment.guestOverpayment : 700;
        const guestExtra = extraGuests * overp * nights;
        total += guestExtra;

        const label = pluralNights(nights);

        el.innerHTML = `
            <div class="row">
                <span>${nights} ${label}</span>
                <span>${currentApartment.pricePerNight.toLocaleString('ru-RU')} ₽/ночь</span>
            </div>
            ${guestExtra > 0 ? `
            <div class="row">
                <span>Доп. гости (${extraGuests} чел. × 700 ₽ × ${nights} ${label})</span>
                <span>+${guestExtra.toLocaleString('ru-RU')} ₽</span>
            </div>` : ''}
            <div class="row total">
                <span>Итого</span>
                <strong>${total.toLocaleString('ru-RU')} ₽</strong>
            </div>`;
        el.style.display = 'block';
    } else {
        el.style.display = 'none';
    }
}

function pluralNights(n) {
    const mod10 = n % 10;
    const mod100 = n % 100;
    if (mod100 >= 11 && mod100 <= 14) return 'ночей';
    if (mod10 === 1) return 'ночь';
    if (mod10 >= 2 && mod10 <= 4) return 'ночи';
    return 'ночей';
}

function switchTab(tab, btn) {
    document.querySelectorAll('.modal-tab-content').forEach(el => el.style.display = 'none');
    document.querySelectorAll('.modal-tab').forEach(el => el.classList.remove('active'));
    document.getElementById(`tab-${tab}`).style.display = 'block';
    if (btn) btn.classList.add('active');
}

// ── Инициализация ────────────────────────────────────────
async function init() {
    showSkeletons();
    await loadSiteSettings();
    apartments = await apiGetApartments();
	await loadAmenityFilter();
    filteredApartments = [...apartments];
    renderGrid(filteredApartments);
    guardNumberInputs();

    // Input listeners for live filter
    ['guests', 'priceMin', 'priceMax'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.addEventListener('input', applyFilters);
    });

    const params = new URLSearchParams(window.location.search);
    const aptId = params.get('apt');
    if (aptId) openModal(parseInt(aptId), true);

    const prices = apartments.map(a => a.pricePerNight);
    const minPrice = Math.min(...prices);
    const maxPrice = Math.max(...prices);

    const priceMin = document.getElementById('priceMin');
    const priceMax = document.getElementById('priceMax');

    if (priceMin) {
        priceMin.min = minPrice;
        priceMin.max = maxPrice;
        priceMin.placeholder = `От ${minPrice.toLocaleString('ru-RU')}`;
    }
    if (priceMax) {
        priceMax.min = minPrice;
        priceMax.max = maxPrice;
        priceMax.placeholder = `До ${maxPrice.toLocaleString('ru-RU')}`;
    }
}

init();

// ── LIGHTBOX ──────────────────────────────────────────────
let lightboxImages = [];
let lightboxIndex = 0;

function openLightbox(images, startIndex) {
    lightboxImages = images;
    lightboxIndex = startIndex;
    renderLightbox();
    document.getElementById('lightbox').classList.add('open');
    document.body.style.overflow = 'hidden';
}

function closeLightbox() {
    document.getElementById('lightbox').classList.remove('open');
    document.body.style.overflow = '';
}

function lightboxBgClick(e) {
    if (e.target === e.currentTarget) closeLightbox();
}

function lightboxStep(dir) {
    lightboxIndex = (lightboxIndex + dir + lightboxImages.length) % lightboxImages.length;
    renderLightbox();
}

function renderLightbox() {
    document.getElementById('lightboxImg').src = lightboxImages[lightboxIndex];
    document.getElementById('lightboxCounter').textContent =
        lightboxImages.length > 1 ? `${lightboxIndex + 1} / ${lightboxImages.length}` : '';
}

document.addEventListener('keydown', e => {
    const lb = document.getElementById('lightbox');
    if (lb.classList.contains('open')) {
        if (e.key === 'ArrowRight') lightboxStep(1);
        if (e.key === 'ArrowLeft') lightboxStep(-1);
        if (e.key === 'Escape') closeLightbox();
        return;
    }
    if (e.key === 'Escape' && document.getElementById('headerActions').classList.contains('open')) {
        closeMobileMenu();
    }
});