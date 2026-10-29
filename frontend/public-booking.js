/* ══════════════════════════════════════════════════════════
public-booking.js — публичная страница просмотра брони
Ожидает параметры: ?id=123&token=abc123xyz
══════════════════════════════════════════════════════════ */

let bookingData = null;
let galleryUrls = [];

// ── Хелперы ─────────────────────────────────────────────
function imgUrl(path) {
    if (!path) return null;
    if (path.startsWith('https') || path.startsWith('http')) return path;
    return API_URL + path;
}

function formatDate(isoStr) {
    if (!isoStr) return '—';
    const d = new Date(isoStr);
    if (isNaN(d.getTime())) return '—';
    const months = [
        'января', 'февраля', 'марта', 'апреля', 'мая', 'июня',
        'июля', 'августа', 'сентября', 'октября', 'ноября', 'декабря'
    ];
    return `${d.getDate()} ${months[d.getMonth()]} ${d.getFullYear()}`;
}

function pluralNights(n) {
    const mod10 = n % 10, mod100 = n % 100;
    if (mod100 >= 11 && mod100 <= 14) return 'ночей';
    if (mod10 === 1) return 'ночь';
    if (mod10 >= 2 && mod10 <= 4) return 'ночи';
    return 'ночей';
}

function statusLabel(status) {
    const map = {
        pending: { text: 'Ожидает подтверждения', cls: 'status-pending', icon: 'fa-clock' },
        confirmed: { text: 'Подтверждено', cls: 'status-confirmed', icon: 'fa-circle-check' },
        cancelled: { text: 'Отменено', cls: 'status-cancelled', icon: 'fa-circle-xmark' },
    };
    return map[status] || { text: status, cls: 'status-pending', icon: 'fa-question' };
}

// ── Загрузка брони ──────────────────────────────────────
async function loadBooking(id, token) {
    const res = await fetch(`${API_URL}/bookings/${id}/public/${token}`);
    if (res.status === 404) {
        throw new Error('Бронирование не найдено. Возможно, оно было отменено или ссылка устарела.');
    }
    if (!res.ok) {
        throw new Error('Не удалось загрузить бронирование. Попробуйте позже.');
    }
    return await res.json();
}

// ── Рендер ──────────────────────────────────────────────
function renderBooking(booking) {
    bookingData = booking;
    const apt = booking.apartment;

    // Заголовок и спеки
    document.getElementById('bookingTitle').textContent = apt.title;
    document.getElementById('bookingSpecs').innerHTML = `
        <span><i class="fa-solid fa-user-group"></i> до ${apt.guests} гостей</span>
        <span><i class="fa-solid fa-ruler-combined"></i> ${apt.area} м²</span>
        <span><i class="fa-solid fa-door-open"></i> ${apt.roomsCount} комн.</span>
    `;

    // Галерея — используем apartmentGallery
    const mainImg = document.getElementById('bookingMainImg');
    const thumbs = document.getElementById('bookingThumbs');
    const gallery = apt.apartmentGallery || [];
    const firstSrc = imgUrl(apt.mainImage) || 'https://placehold.co/600x340?text=Нет+фото';
    mainImg.src = firstSrc;

    galleryUrls = gallery.length
        ? gallery.map(g => imgUrl(g.imagePath)).filter(Boolean)
        : [firstSrc];

    mainImg.onclick = () => {
        const idx = galleryUrls.indexOf(mainImg.src);
        openLightbox(galleryUrls, idx < 0 ? 0 : idx);
    };

    thumbs.innerHTML = '';
    if (gallery.length > 1) {
        gallery.forEach(g => {
            const src = imgUrl(g.imagePath);
            if (!src) return;
            const img = document.createElement('img');
            img.src = src;
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

    // Описание
    document.getElementById('bookingDesc').textContent = apt.description || 'Описание не указано';

    // Удобства — используем apartmentAmenities
    const amenities = apt.apartmentAmenities || [];
    document.getElementById('bookingAmenities').innerHTML = amenities.length
        ? amenities.map(a => `<span class="amenity-tag"><i class="fa-solid ${a.icon || 'fa-check'}"></i>${a.name}</span>`).join('')
        : '<p style="color:var(--ink-3);">Не указано</p>';

    // Рядом
    const nearby = apt.nearby || [];
    document.getElementById('bookingNearby').innerHTML = nearby.length
        ? nearby.map(n => {
            const inner = `
                <i class="fa-solid ${n.icon || 'fa-location-dot'}"></i>
                <span>${n.name}</span>
                <span class="modal-nearby-dist">${n.distance || ''}</span>
                ${n.wikiPlaceId ? '<i class="fa-solid fa-arrow-up-right-from-square modal-nearby-arrow"></i>' : ''}`;
            return n.wikiPlaceId
                ? `<a class="modal-nearby linked" href="wiki.html?place=${n.wikiPlaceId}">${inner}</a>`
                : `<div class="modal-nearby">${inner}</div>`;
        }).join('')
        : '<p style="color:var(--ink-3);">Не указано</p>';

    // Правила
    const rules = apt.rules || [];
    document.getElementById('bookingRules').innerHTML = rules.length
        ? rules.map(r => `
            <div class="modal-rule">
                <i class="fa-solid ${r.icon || 'fa-circle-info'}"></i>
                <span>${r.text}</span>
            </div>`).join('')
        : '<p style="color:var(--ink-3);">Правила не указаны</p>';

    // Карточка брони
    const nights = Math.max(1, Math.round((new Date(booking.endDate) - new Date(booking.startDate)) / 86_400_000));
    const rows = [
        `<div class="row"><span>Заезд</span><strong>${formatDate(booking.startDate)}</strong></div>`,
        `<div class="row"><span>Выезд</span><strong>${formatDate(booking.endDate)}</strong></div>`,
        `<div class="row"><span>Длительность</span><strong>${nights} ${pluralNights(nights)}</strong></div>`,
        `<div class="row"><span>Гостей</span><strong>${booking.guests}</strong></div>`,
    ];
    if (booking.fullName) {
        rows.push(`<div class="row"><span>Гость</span><strong>${booking.fullName}</strong></div>`);
    }
    rows.push(`<div class="row total"><span>Итого оплачено</span><strong>${(booking.totalPrice || 0).toLocaleString('ru-RU')} ₽</strong></div>`);
    document.getElementById('bookingSummary').innerHTML = rows.join('');

    // Статус
    const st = statusLabel(booking.status);
    document.getElementById('bookingStatusBlock').innerHTML = `
        <span class="status-badge ${st.cls}">
            <i class="fa-solid ${st.icon}"></i> ${st.text}
        </span>
    `;

    // Авторизация
    if (typeof isLoggedIn === 'function' && isLoggedIn()) {
        document.getElementById('bookingOwnedHint').style.display = 'block';
    } else {
        document.getElementById('bookingAuthGate').style.display = 'block';
    }

    // Показываем контент
    document.getElementById('bookingLoading').style.display = 'none';
    document.getElementById('bookingContent').style.display = 'grid';
}

function showError(msg) {
    document.getElementById('bookingLoading').style.display = 'none';
    document.getElementById('bookingErrorMsg').textContent = msg;
    document.getElementById('bookingError').style.display = 'flex';
}

// ── Табы ────────────────────────────────────────────────
function switchBookingTab(tab, btn) {
    document.querySelectorAll('.modal-tab-content').forEach(el => el.style.display = 'none');
    document.querySelectorAll('.modal-tab').forEach(el => el.classList.remove('active'));
    document.getElementById(`tab-${tab}`).style.display = 'block';
    if (btn) btn.classList.add('active');
}

// ── Копирование ссылки ──────────────────────────────────
function copyBookingLink() {
    const url = window.location.href;
    const btn = document.querySelector('.btn-copy-link');

    const doCopy = () => {
        if (btn) {
            btn.classList.add('copied');
            btn.innerHTML = '<i class="fa-solid fa-check"></i> Ссылка скопирована';
            setTimeout(() => {
                btn.classList.remove('copied');
                btn.innerHTML = '<i class="fa-solid fa-link"></i> Скопировать ссылку';
            }, 2000);
        }
        showToast('Ссылка скопирована');
    };

    if (navigator.clipboard) {
        navigator.clipboard.writeText(url).then(doCopy).catch(() => fallbackCopy(url, doCopy));
    } else {
        fallbackCopy(url, doCopy);
    }
}

function fallbackCopy(text, cb) {
    const input = document.createElement('input');
    input.value = text;
    document.body.appendChild(input);
    input.select();
    try { document.execCommand('copy'); } catch (e) { }
    input.remove();
    if (cb) cb();
}

function showToast(msg) {
    let toast = document.querySelector('.booking-toast');
    if (!toast) {
        toast = document.createElement('div');
        toast.className = 'booking-toast';
        document.body.appendChild(toast);
    }
    toast.textContent = msg;
    toast.classList.add('show');
    clearTimeout(toast._hideTimer);
    toast._hideTimer = setTimeout(() => toast.classList.remove('show'), 2500);
}

// ── Lightbox ────────────────────────────────────────────
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
    if (lb && lb.classList.contains('open')) {
        if (e.key === 'ArrowRight') lightboxStep(1);
        if (e.key === 'ArrowLeft') lightboxStep(-1);
        if (e.key === 'Escape') closeLightbox();
    }
});

// ── Инициализация ───────────────────────────────────────
async function init() {
    const params = new URLSearchParams(window.location.search);
    const id = params.get('id');
    const token = params.get('token');

    if (!id || !token) {
        showError('Ссылка повреждена: отсутствуют параметры бронирования.');
        return;
    }

    try {
        const booking = await loadBooking(id, token);
        renderBooking(booking);
    } catch (err) {
        showError(err.message || 'Не удалось загрузить бронирование.');
    }
}

init();