if (typeof isLoggedIn === 'function' && isLoggedIn()) {
        location.href = 'index.html';
    }

    document.getElementById('registerForm').addEventListener('submit', (e) => {
        e.preventDefault();
        doRegister();
    });

    const phoneInput = document.getElementById('regPhone');
    if (phoneInput) {
        IMask(phoneInput, {
            mask: '+{7} (000) 000-00-00',
            lazy: false,
            placeholderChar: '_'
        });
    }

    function validateEmail(email) {
        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
    }
    function validatePhone(phone) {
        return phone.replace(/\D/g, '').length === 11;
    }
    function togglePw(id, btn) {
        const inp = document.getElementById(id);
        const isText = inp.type === 'text';
        inp.type = isText ? 'password' : 'text';
        btn.querySelector('i').className = isText ? 'fa-solid fa-eye' : 'fa-solid fa-eye-slash';
    }

    function showError(msg, field = null) {
        const errorEl  = document.getElementById('regError');
        document.getElementById('regErrorText').textContent = msg;
        errorEl.classList.add('show');
        if (field) {
            field.style.borderColor = 'var(--red)';
            field.focus();
        }
    }

    async function doRegister() {
        const fields = {
            name:  document.getElementById('regName'),
            phone: document.getElementById('regPhone'),
            email: document.getElementById('regEmail'),
            pass:  document.getElementById('regPassword')
        };
        const errorEl = document.getElementById('regError');
        const btn     = document.getElementById('regBtn');

        errorEl.classList.remove('show');
        Object.values(fields).forEach(f => f.style.borderColor = '');

        if (!fields.name.value.trim())                   return showError('Введите имя', fields.name);
        if (!validateEmail(fields.email.value.trim()))   return showError('Введите корректный Email адрес', fields.email);
        if (!validatePhone(fields.phone.value))          return showError('Введите номер телефона полностью', fields.phone);
        if (fields.pass.value.length < 6)               return showError('Пароль должен быть минимум 6 символов', fields.pass);

        btn.disabled = true;
        btn.textContent = 'Создаём аккаунт...';

        try {
            const cleanPhone = fields.phone.value.replace(/\D/g, '');
            const res = await fetch(`${API_URL}/auth/register`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    fullName:    fields.name.value.trim(),
                    phoneNumber: cleanPhone,
                    email:       fields.email.value.trim(),
                    password:    fields.pass.value
                })
            });
            const data = await res.json();
            if (!res.ok) throw new Error(data.message || 'Ошибка регистрации');
            if (data.token) {
			if (typeof setAuth === 'function') setAuth(data);
				location.href = 'index.html';
			} else {
				// Показываем сообщение о подтверждении email
				const card = document.querySelector('.auth-card');
				card.innerHTML = `
					<div style="text-align:center;padding:20px 0;">
						<i class="fa-solid fa-envelope-circle-check" style="font-size:3rem;color:var(--primary);margin-bottom:16px;display:block;"></i>
						<h2>Проверьте почту!</h2>
						<p style="color:var(--ink-3);margin-top:8px;">
							Мы отправили письмо на <b>${fields.email.value.trim()}</b>.<br>
							Перейдите по ссылке в письме чтобы завершить регистрацию.
						</p>
						<a href="index.html" style="display:inline-block;margin-top:24px;" class="auth-submit">На главную</a>
					</div>
				`;
			}
        } catch (err) {
            showError(err.message);
        } finally {
            btn.disabled = false;
            btn.textContent = 'Зарегистрироваться';
        }
    }