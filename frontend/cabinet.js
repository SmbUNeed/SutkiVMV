if (!isLoggedIn()) location.href = 'login.html';

/* ── Мобильное меню (шапка) ───────────────────────────── */
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
window.addEventListener('resize', () => {
    if (window.innerWidth > 680) closeMobileMenu();
});

function showTab(name, el) {
    document.querySelectorAll('.cabinet-section').forEach(s => s.classList.remove('active'));
    document.querySelectorAll('.cabinet-nav-item').forEach(n => n.classList.remove('active'));
    document.getElementById(`tab-${name}`).classList.add('active');
    el.classList.add('active');
}

function togglePw(id, btn) {
    const inp = document.getElementById(id);
    const isText = inp.type === 'text';
    inp.type = isText ? 'password' : 'text';
    btn.querySelector('i').className = isText ? 'fa-solid fa-eye' : 'fa-solid fa-eye-slash';
}

async function loadProfile() {
    const user = getUser();
    if (!user) return;

    const initials = (user.fullName || user.email)
        .split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase() || '?';
    document.getElementById('cabinetAvatarLetters').textContent = initials;
    document.getElementById('cabinetName').textContent  = user.fullName || 'Пользователь';
    document.getElementById('cabinetEmail').textContent = user.email;

    try {
        const res = await authFetch('/auth/me');
        if (res.ok) {
            const fresh = await res.json();
            const auth = getAuth();
            auth.user = { ...auth.user, ...fresh };
            setAuth(auth);

            document.getElementById('pfName').value  = fresh.fullName    || '';
            document.getElementById('pfPhone').value = fresh.phoneNumber || '';
            document.getElementById('pfEmail').value = fresh.email       || '';
            document.getElementById('cabinetName').textContent  = fresh.fullName || 'Пользователь';
            document.getElementById('cabinetEmail').textContent = fresh.email;

            const i2 = (fresh.fullName || fresh.email).split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase();
            document.getElementById('cabinetAvatarLetters').textContent = i2 || '?';
        }
    } catch {}
}

async function saveProfile() {
    const fullName    = document.getElementById('pfName').value.trim();
    const phoneNumber = document.getElementById('pfPhone').value.trim();
    const newPassword = document.getElementById('pfPassword').value || null;
    const btn         = document.getElementById('pfSaveBtn');
    const successEl   = document.getElementById('profileSuccess');
    successEl.classList.remove('show');

    if (!fullName || !phoneNumber) { alert('Заполните имя и телефон'); return; }
    if (newPassword && newPassword.length < 6) { alert('Пароль должен быть минимум 6 символов'); return; }

    btn.disabled = true;
    btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Сохраняем...';

    try {
        const res = await authFetch('/auth/me', {
            method: 'PUT',
            body: JSON.stringify({ fullName, phoneNumber, newPassword })
        });
        if (!res.ok) {
            const d = await res.json();
            throw new Error(d.message || 'Ошибка сохранения');
        }
        const updated = await res.json();
        const auth = getAuth();
        auth.user = { ...auth.user, fullName: updated.fullName, phoneNumber: updated.phoneNumber };
        setAuth(auth);
        renderHeaderUser();

        document.getElementById('pfPassword').value = '';
        document.getElementById('cabinetName').textContent = updated.fullName || 'Пользователь';
        const i2 = (updated.fullName || updated.email || '').split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase();
        document.getElementById('cabinetAvatarLetters').textContent = i2 || '?';

        successEl.classList.add('show');
        setTimeout(() => successEl.classList.remove('show'), 4000);
    } catch (err) {
        alert(err.message);
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="fa-solid fa-floppy-disk"></i> Сохранить изменения';
    }
}

async function loadBookings() {
    const list = document.getElementById('bookingsList');
    try {
        const res = await authFetch('/auth/my-bookings');
        if (!res.ok) throw new Error('Ошибка загрузки бронирований');
        const bookings = await res.json();

        if (!bookings.length) {
            list.innerHTML = `
                <div class="empty-state">
                    <i class="fa-regular fa-calendar-xmark"></i>
                    <p>У вас пока нет бронирований.<br>
                       <a href="index.html" style="color:var(--primary);font-weight:700;">Посмотреть квартиры →</a>
                    </p>
                </div>`;
            return;
        }

        list.innerHTML = bookings.map(b => {
            const imgSrc = b.apartmentImage ? API_BASE + b.apartmentImage : null;
            const start  = new Date(b.startDate).toLocaleDateString('ru-RU');
            const end    = new Date(b.endDate).toLocaleDateString('ru-RU');
            const nights = b.nights;
            const label  = nights === 1 ? 'ночь' : nights < 5 ? 'ночи' : 'ночей';

            const statusMap = {
                'Pending':   ['status-pending',   'Ожидание'],
                'Confirmed': ['status-confirmed', 'Подтверждено'],
                'Cancelled': ['status-cancelled', 'Отменено']
            };
            const [cls, text] = statusMap[b.status] ?? ['', b.status];

            return `
                <div class="booking-card">
                    ${imgSrc
                        ? `<img class="booking-card-img" src="${imgSrc}" alt="${b.apartmentTitle}" loading="lazy">`
                        : `<div class="booking-card-img-placeholder"><i class="fa-solid fa-image"></i></div>`}
                    <div class="booking-card-info">
                        <div class="booking-card-title">${b.apartmentTitle}</div>
                        <div class="booking-card-dates">
                            <i class="fa-solid fa-calendar" style="color:var(--primary)"></i>
                            ${start} — ${end} &nbsp;·&nbsp; ${nights} ${label}
                        </div>
                        <div class="booking-card-footer">
                            <div>
                                <span class="booking-card-price">${b.totalPrice.toLocaleString('ru-RU')} ₽</span>
                                ${b.pricePerNight ? `<span class="booking-card-nights"> (${b.pricePerNight.toLocaleString('ru-RU')} ₽/ночь)</span>` : ''}
                            </div>
                            <span class="status-badge ${cls}">${text}</span>
                        </div>
                    </div>
                </div>`;
        }).join('');
    } catch (err) {
        list.innerHTML = `<div class="empty-state"><i class="fa-solid fa-circle-exclamation"></i><p>${err.message}</p></div>`;
    }
}

document.addEventListener('DOMContentLoaded', () => {
    loadProfile();
    loadBookings();
});