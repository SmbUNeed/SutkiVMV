if (isLoggedIn()) location.href = 'index.html';

function togglePw(id, btn) {
    const inp = document.getElementById(id);
    const isText = inp.type === 'text';
    inp.type = isText ? 'password' : 'text';
    btn.querySelector('i').className = isText ? 'fa-solid fa-eye' : 'fa-solid fa-eye-slash';
}

function showError(msg) {
    const errorEl = document.getElementById('loginError');
    document.getElementById('loginErrorText').textContent = msg;
    errorEl.classList.add('show');
}

document.getElementById('loginForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const email    = document.getElementById('loginEmail').value.trim();
    const password = document.getElementById('loginPassword').value;
    const errorEl  = document.getElementById('loginError');
    const btn      = document.getElementById('loginBtn');
    errorEl.classList.remove('show');

    if (!email || !password) {
        showError('Заполните все поля'); return;
    }

    btn.disabled = true;
    btn.textContent = 'Входим...';

    try {
        const res  = await fetch(`${API_URL}/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, password })
        });
        const data = await res.json();
        if (!res.ok) throw new Error(data.message || 'Неверный email или пароль');
        setAuth(data);
        location.href = 'index.html';
    } catch (err) {
        showError(err.message);
    } finally {
        btn.disabled = false;
        btn.textContent = 'Войти';
    }
});

document.addEventListener('keydown', e => { if (e.key === 'Enter') doLogin(); });