/* ══════════════════════════════════════════════════════════
   auth.js — общий модуль аутентификации v2
   Подключается на всех страницах сайта
   ══════════════════════════════════════════════════════════ */

const API_BASE = 'https://sutkivmv.ru';
const API_URL  = `${API_BASE}/api`;

// ── Хранилище состояния ──────────────────────────────────
function getAuth() {
    try {
        const raw = localStorage.getItem('sutkivmv_auth');
        return raw ? JSON.parse(raw) : null;
    } catch { return null; }
}
function setAuth(data)  { localStorage.setItem('sutkivmv_auth', JSON.stringify(data)); }
function clearAuth()    { localStorage.removeItem('sutkivmv_auth'); }
function getToken()     { return getAuth()?.token ?? null; }
function getUser()      { return getAuth()?.user  ?? null; }
function isLoggedIn()   { return !!getToken(); }

// ── API с авторизацией ───────────────────────────────────
async function authFetch(url, options = {}) {
    const token   = getToken();
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
            ? `<a href="admin.html" class="btn-admin" title="Панель администратора">
                   <i class="fa-solid fa-shield-halved"></i><span> Админка</span>
               </a>`
            : '';

        container.innerHTML = `
            <div class="user-menu">
                ${adminLink}
                <a href="cabinet.html" class="user-avatar" title="Личный кабинет">${initials}</a>
                <a href="cabinet.html" class="user-name">${user.fullName || user.email}</a>
                <button class="btn-logout" onclick="logout()">
                    <i class="fa-solid fa-arrow-right-from-bracket"></i><span class="hide-xs"> Выйти</span>
                </button>
            </div>`;
    } else {
        container.innerHTML = `
            <div class="header-user" style="display:flex;align-items:center;gap:8px;">
                <a href="login.html" class="btn-auth btn-auth-ghost">Войти</a>
            </div>`;
    }
}

function logout() {
    clearAuth();
    renderHeaderUser();
    if (location.pathname.includes('cabinet')) {
        location.href = 'index.html';
    }
}

// ── Тема (день/ночь) ─────────────────────────────────────
function applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('sutkivmv_theme', theme);
}

function toggleTheme() {
    const current = document.documentElement.getAttribute('data-theme') || 'light';
    applyTheme(current === 'dark' ? 'light' : 'dark');
}

function initTheme() {
    // Восстановить сохранённую тему или использовать системную
    const saved = localStorage.getItem('sutkivmv_theme');
    if (saved) {
        applyTheme(saved);
    } else {
        const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
        applyTheme(prefersDark ? 'dark' : 'light');
    }
}

// ── Инициализация ────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    initTheme();
    renderHeaderUser();

    // Привязать кнопку переключения темы (если есть на странице)
    const toggleBtn = document.getElementById('themeToggle');
    if (toggleBtn) {
        toggleBtn.addEventListener('click', toggleTheme);
    }
});
