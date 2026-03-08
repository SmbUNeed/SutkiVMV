/* ══════════════════════════════════════════════════════════
   auth.js — общий модуль аутентификации
   Подключается на всех страницах сайта
   ══════════════════════════════════════════════════════════ */

const API_BASE = 'http://157.22.230.22';
const API_URL  = `${API_BASE}/api`;

// ── Хранилище состояния ──────────────────────────────────

function getAuth() {
    try {
        const raw = localStorage.getItem('sutkivmv_auth');
        return raw ? JSON.parse(raw) : null;
    } catch { return null; }
}

function setAuth(data) {
    localStorage.setItem('sutkivmv_auth', JSON.stringify(data));
}

function clearAuth() {
    localStorage.removeItem('sutkivmv_auth');
}

function getToken() { return getAuth()?.token ?? null; }
function getUser()  { return getAuth()?.user  ?? null; }
function isLoggedIn() { return !!getToken(); }

// ── API с авторизацией ───────────────────────────────────

async function authFetch(url, options = {}) {
    const token = getToken();
    const headers = { 'Content-Type': 'application/json', ...(options.headers ?? {}) };
    if (token) headers['Authorization'] = `Bearer ${token}`;
    const res = await fetch(`${API_URL}${url}`, { ...options, headers });
    if (res.status === 401) { clearAuth(); renderHeaderUser(); }
    return res;
}

// ── Отрисовка шапки ─────────────────────────────────────

function renderHeaderUser() {
    const container = document.getElementById('headerUser');
    if (!container) return;

    const user = getUser();
    if (user) {
        const initials = (user.fullName || user.email)
            .split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase() || '?';

        const adminLink = user.role === 'Admin'
            ? `<a href="../admin.html" class="btn-admin" title="Панель администратора">
                   <i class="fa-solid fa-shield-halved"></i> Админка
               </a>`
            : '';

        container.innerHTML = `
            <div class="user-menu">
                ${adminLink}
                <a href="cabinet.html" class="user-avatar" title="Личный кабинет">${initials}</a>
                <a href="cabinet.html" class="user-name">${user.fullName || user.email}</a>
                <button class="btn-logout" onclick="logout()">
                    <i class="fa-solid fa-arrow-right-from-bracket"></i> Выйти
                </button>
            </div>
        `;
    } else {
        container.innerHTML = `
            <div class="header-user">
                <a href="login.html" class="btn-auth btn-auth-ghost">Войти</a>
                <a href="register.html" class="btn-auth btn-auth-primary">Регистрация</a>
            </div>
        `;
    }
}

function logout() {
    clearAuth();
    renderHeaderUser();
    // Если мы на странице кабинета — редиректим
    if (location.pathname.includes('cabinet')) {
        location.href = 'index.html';
    }
}

// ── Инициализация шапки при загрузке ────────────────────
document.addEventListener('DOMContentLoaded', renderHeaderUser);