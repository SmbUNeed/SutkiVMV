document.addEventListener('DOMContentLoaded', async () => {
    const params = new URLSearchParams(location.search);
    const token  = params.get('token');
    const email  = params.get('email');

    const icon  = document.getElementById('statusIcon');
    const title = document.getElementById('statusTitle');
    const text  = document.getElementById('statusText');
    const link  = document.getElementById('statusLink');

    if (!token || !email) {
        icon.innerHTML  = '<i class="fa-solid fa-circle-xmark" style="color:#ef4444;"></i>';
        title.textContent = 'Неверная ссылка';
        text.textContent  = 'Ссылка недействительна. Попробуйте зарегистрироваться снова.';
        link.style.display = 'inline-block';
        return;
    }

    try {
        const res = await fetch(`${API_URL}/auth/confirm-email`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email, token })
        });

        if (res.ok) {
            icon.innerHTML    = '<i class="fa-solid fa-circle-check" style="color:#22c55e;"></i>';
            title.textContent = 'Email подтверждён!';
            text.textContent  = 'Спасибо! Теперь вы можете пользоваться всеми функциями сайта.';
        } else {
            const data = await res.json();
            icon.innerHTML    = '<i class="fa-solid fa-circle-xmark" style="color:#ef4444;"></i>';
            title.textContent = 'Ошибка подтверждения';
            text.textContent  = data.message || 'Ссылка недействительна или истекла.';
        }
    } catch {
        icon.innerHTML    = '<i class="fa-solid fa-circle-xmark" style="color:#ef4444;"></i>';
        title.textContent = 'Ошибка соединения';
        text.textContent  = 'Не удалось связаться с сервером. Попробуйте позже.';
    }

    link.style.display = 'inline-block';
});