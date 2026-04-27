'use strict';

    const API  = 'https://sutkivmv.ru/api';
    const BASE = 'https://sutkivmv.ru';

    // Глобальное состояние авторизации — заполняется в INIT
    let auth = null;

    function imgPath(p) {
        if (!p) return null;
        return p.startsWith('http') ? p : BASE + p;
    }

    /* ── NAVIGATION ──────────────────────────────────────── */
    function showSec(id, el) {
        document.querySelectorAll('.sec').forEach(s => s.classList.remove('active'));
        document.querySelectorAll('.ni').forEach(n => n.classList.remove('active'));
        document.getElementById(id).classList.add('active');
        if (el) el.classList.add('active');
        if (id === 'list-apt') loadGrid();
        if (id === 'bookings') loadBookings();
        if (id === 'clients')  loadClients();
        if (id === 'rules')    loadRules();
        if (id === 'nearby')   loadNearby();
        if (id === 'wiki-places') loadWikiPlaces();
    }

    /* ── TOAST ───────────────────────────────────────────── */
    let _tt;
    function toast(msg, type = 'ok') {
        const el = document.getElementById('toast');
        el.textContent = msg;
        el.className = `on ${type}`;
        clearTimeout(_tt);
        _tt = setTimeout(() => { el.className = ''; }, 3500);
    }

    /* ── API ─────────────────────────────────────────────── */
    async function api(url, opts = {}) {
        const res = await fetch(API + url, {
            headers: { 'Content-Type': 'application/json' },
            ...opts
        });
        if (!res.ok) {
            const b = await res.json().catch(() => ({}));
            throw new Error(b.message || `HTTP ${res.status}`);
        }
        if (res.status === 204) return null;
        return res.json();
    }

    /* ── PHOTO PREVIEW ───────────────────────────────────── */
    function previewPhotos(ev, containerId, withRadio) {
        const c = document.getElementById(containerId);
        c.innerHTML = '';
        Array.from(ev.target.files).forEach((f, i) => {
            const r = new FileReader();
            r.onload = e => {
                const d = document.createElement('div');
                d.className = 'pi' + (i === 0 ? ' main' : '');
                d.innerHTML = `
                <img src="${e.target.result}" alt="">
                ${withRadio ? `<div class="pi-ft">
                    <input type="radio" name="mainPhoto" value="${i}" ${i === 0 ? 'checked' : ''}
                        onchange="setMainPrev(this)">
                    <span>${i === 0 ? '★ Главное' : 'Главное'}</span>
                </div>` : ''}`;
                c.appendChild(d);
            };
            r.readAsDataURL(f);
        });
    }

    function setMainPrev(radio) {
        const idx = parseInt(radio.value);
        document.querySelectorAll('#previewAdd .pi').forEach((el, i) => {
            el.classList.toggle('main', i === idx);
            const sp = el.querySelector('span');
            if (sp) sp.textContent = i === idx ? '★ Главное' : 'Главное';
        });
    }

    /* ── УДОБСТВА ────────────────────────────────────────── */
    let amenities = [];
    let rulesCatalog = [];
    let nearbyPlacesCatalog = [];

    async function loadRulesCatalog() {
        try { rulesCatalog = await api('/apartments/rules'); }
        catch { rulesCatalog = []; }
    }

    async function loadNearbyPlacesCatalog() {
        try { nearbyPlacesCatalog = await api('/apartments/nearby-places'); }
        catch { nearbyPlacesCatalog = []; }
    }

    function populateRuleSelect(selectedId = null) {
        const sel = document.getElementById('ruleSelect');
        if (!sel) return;
        sel.innerHTML = rulesCatalog.length
            ? rulesCatalog.map(r =>
                `<option value="${r.id}" ${r.id === selectedId ? 'selected' : ''}>
                    ${r.text}
                </option>`).join('')
            : '<option value="" disabled>Нет правил в каталоге</option>';
    }

    function populateNearbySelect(selectedId = null) {
        const sel = document.getElementById('nearbySelect');
        if (!sel) return;
        sel.innerHTML = nearbyPlacesCatalog.length
            ? nearbyPlacesCatalog.map(n =>
                `<option value="${n.id}" ${n.id === selectedId ? 'selected' : ''}>
                    ${n.name}
                </option>`).join('')
            : '<option value="" disabled>Нет мест в каталоге</option>';
    }

    async function loadAmenities() {
        try { amenities = await api('/amenities'); }
        catch { amenities = []; }
    }

    function renderAmen(containerId, selectedIds = []) {
        const el = document.getElementById(containerId);
        if (!amenities.length) {
            el.innerHTML = '<span style="color:var(--tm);font-size:.85rem;">Нет удобств</span>';
            return;
        }
        el.innerHTML = amenities.map(a => {
            const on = selectedIds.includes(a.id);
            return `<label class="al ${on ? 'on' : ''}">
            <input type="checkbox" value="${a.id}" ${on ? 'checked' : ''}
                onchange="this.closest('.al').classList.toggle('on',this.checked)">
            <i class="fa-solid ${a.icon || 'fa-check'}" style="color:var(--a);font-size:11px;"></i>
            ${a.name}
        </label>`;
        }).join('');
    }

    function getChecked(containerId) {
        return [...document.querySelectorAll(`#${containerId} input[type=checkbox]:checked`)]
            .map(cb => parseInt(cb.value));
    }

    async function addAmen() {
        const name = document.getElementById('newAmenName').value.trim();
        const icon = document.getElementById('newAmenIcon').value;
        if (!name) { toast('Введите название удобства', 'er'); return; }
        try {
            const a = await api('/amenities', { method: 'POST', body: JSON.stringify({ name, icon }) });
            amenities.push(a);
            document.getElementById('newAmenName').value = '';
            renderAmen('amenAdd',  [...getChecked('amenAdd'),  a.id]);
            renderAmen('amenEdit', [...getChecked('amenEdit'), a.id]);
            toast(`Удобство «${a.name}» добавлено`);
        } catch (err) { toast(err.message, 'er'); }
    }

    /* ── СОЗДАНИЕ КВАРТИРЫ ───────────────────────────────── */
    async function createApt(e) {
        e.preventDefault();
        const btn = document.getElementById('btnCreate');
        btn.disabled = true;
        btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Создание...';

        try {
            const apt = await api('/apartments/new', {
                method: 'POST',
                body: JSON.stringify({
                    title:         document.getElementById('aTitle').value.trim(),
                    address:       document.getElementById('aAddr').value.trim(),
                    pricePerNight: parseFloat(document.getElementById('aPrice').value),
                    guests:        parseInt(document.getElementById('aGuests').value),
                    area:          parseFloat(document.getElementById('aArea').value),
                    roomsCount:    parseInt(document.getElementById('aRooms').value),
                    description:   document.getElementById('aDesc').value.trim(),
                    isActive:      true,
                    isDeleted:     false
                })
            });

            // Удобства
            const ids = getChecked('amenAdd');
            if (ids.length) {
                await api(`/apartments/${apt.id}/amenities`, {
                    method: 'POST', body: JSON.stringify({ amenityIds: ids })
                });
            }

            // Фото
            const files = document.getElementById('aFiles').files;
            if (files.length) {
                const mainIdx = parseInt(
                    document.querySelector('#previewAdd input[name="mainPhoto"]:checked')?.value ?? 0
                );
                const fd = new FormData();
                for (const f of files) fd.append('files', f);
                fd.append('mainPhotoIndex', mainIdx);
                const r = await fetch(`${API}/apartments/${apt.id}/photos`, {
                    method: 'POST',
                    body: fd,
                    headers: { 'Authorization': `Bearer ${auth?.token}` }
                });
                if (!r.ok) console.warn('Фото не загружено:', r.status);
            }

            toast('Квартира успешно создана!');
            document.getElementById('formAdd').reset();
            document.getElementById('previewAdd').innerHTML = '';
            renderAmen('amenAdd', []);
        } catch (err) {
            toast(err.message, 'er');
        } finally {
            btn.disabled = false;
            btn.innerHTML = '<i class="fa-solid fa-check"></i> Создать квартиру';
        }
    }

    /* ── ПОИСК КЛИЕНТА ───────────────────────────────────── */
    let _cst;

    function searchCli(q) {
        clearTimeout(_cst);
        const dd = document.getElementById('cliDrop');
        if (!q || q.length < 2) { dd.style.display = 'none'; return; }

        _cst = setTimeout(async () => {
            try {
                const list = await api(`/clients/search?q=${encodeURIComponent(q)}`);
                dd.innerHTML = list.length
                    ? list.map(c =>
                        `<div class="di" onclick="selCli(${c.id},'${c.fullName.replace(/'/g,"\\'")}','${c.phoneNumber}')">
                        <strong>${c.fullName}</strong>
                        <span style="color:var(--tm);margin-left:8px;">${c.phoneNumber}</span>
                        ${c.email ? `<span style="color:var(--tm);font-size:.8rem;margin-left:6px;">${c.email}</span>` : ''}
                    </div>`).join('')
                    : '<div style="padding:12px 14px;color:var(--tm);font-size:.85rem;">Не найдено</div>';
                dd.style.display = 'block';
            } catch { dd.style.display = 'none'; }
        }, 300);
    }

    function selCli(id, name, phone) {
        document.getElementById('bookCli').value = id;
        document.getElementById('cliSearch').value = `${name} — ${phone}`;
        document.getElementById('cliDrop').style.display = 'none';
        const chip = document.getElementById('cliChip');
        chip.innerHTML = `
        <span><i class="fa-solid fa-user-check" style="color:var(--grn);margin-right:7px;"></i>
        <strong>${name}</strong> &nbsp;${phone}</span>
        <button type="button" class="cx" onclick="clearCli()"><i class="fa-solid fa-xmark"></i></button>`;
        chip.classList.add('on');
    }

    function clearCli() {
        document.getElementById('bookCli').value = '';
        document.getElementById('cliSearch').value = '';
        document.getElementById('cliDrop').style.display = 'none';
        document.getElementById('cliChip').classList.remove('on');
        document.getElementById('cliSearch').focus();
    }

    document.addEventListener('click', e => {
        const dd = document.getElementById('cliDrop');
        if (dd && !dd.contains(e.target) && e.target.id !== 'cliSearch')
            dd.style.display = 'none';
    });

    /* ── СЕТКА КВАРТИР ───────────────────────────────────── */
    async function loadGrid() {
        const c = document.getElementById('aptGrid');
        c.innerHTML = '<div class="ld"><i class="fa-solid fa-spinner fa-spin"></i> Загрузка...</div>';
        try {
            const list = await api('/apartments/all');
            if (!list.length) {
                c.innerHTML = '<div class="es"><i class="fa-solid fa-house"></i><p>Квартир пока нет</p></div>';
                return;
            }
            c.innerHTML = list.map(a => {
                const src = imgPath(a.mainImage);
                return `<div class="ac" id="ac-${a.id}">
                ${src
                    ? `<img src="${src}" alt="${a.title}" loading="lazy">`
                    : `<div class="ac-ph"><i class="fa-solid fa-image"></i></div>`}
                <div class="ac-body">
                    <div class="ac-name">${a.title}</div>
                    <div class="ac-price">${a.pricePerNight.toLocaleString('ru-RU')} ₽/ночь</div>
                    <div class="ac-addr"><i class="fa-solid fa-location-dot" style="color:var(--a);margin-right:4px;"></i>${a.address}</div>
                    ${a.isDeleted
                    ? '<span class="badge br">Удалена</span>'
                    : a.isActive
                        ? '<span class="badge bg">Активна</span>'
                        : '<span class="badge bz">Скрыта</span>'}
                    <div class="ac-foot">
                        <button class="btn bi sm" onclick="openEdit(${a.id})">
                            <i class="fa-solid fa-pen"></i> Изменить
                        </button>
                        ${a.isDeleted
                    ? `<button class="btn bs sm" onclick="restoreApt(${a.id})">
                                <i class="fa-solid fa-rotate-left"></i> Восстановить</button>`
                    : `<button class="btn bd sm" onclick="deleteApt(${a.id})">
                                <i class="fa-solid fa-trash"></i></button>`}
                    </div>
                </div>
            </div>`;
            }).join('');
        } catch (err) {
            c.innerHTML = `<div class="es"><i class="fa-solid fa-circle-exclamation"></i><p>${err.message}</p></div>`;
        }
    }

    async function deleteApt(id) {
        if (!confirm('Скрыть квартиру с сайта?')) return;
        try {
            await api(`/apartments/${id}`, { method: 'DELETE' });
            toast('Квартира удалена');
            loadGrid();
        } catch (err) { toast(err.message, 'er'); }
    }

    async function restoreApt(id) {
        try {
            await api(`/apartments/restore/${id}`, { method: 'PATCH' });
            toast('Квартира восстановлена');
            loadGrid();
        } catch (err) { toast(err.message, 'er'); }
    }

    /* ── РЕДАКТИРОВАНИЕ ──────────────────────────────────── */
    async function openEdit(id) {
        document.getElementById('editModal').classList.add('open');
        document.getElementById('previewEdit').innerHTML = '';
        document.getElementById('eFiles').value = '';
        document.getElementById('editPhotos').innerHTML = '<p style="color:var(--tm);font-size:.85rem;">Загрузка...</p>';
        document.getElementById('amenEdit').innerHTML = '<span style="color:var(--tm);font-size:.85rem;">Загрузка...</span>';

        try {
            const a = await api(`/apartments/${id}`);
            document.getElementById('eId').value     = a.id;
            document.getElementById('eTitle').value  = a.title;
            document.getElementById('eAddr').value   = a.address;
            document.getElementById('ePrice').value  = a.pricePerNight;
            document.getElementById('eGuests').value = a.guests;
            document.getElementById('eArea').value   = a.area;
            document.getElementById('eRooms').value  = a.roomsCount;
            document.getElementById('eDesc').value   = a.description ?? '';

            // amenities — List<AmenityDto> (id, name, icon)
            const selIds = (a.amenities || []).map(x => x.id);
            renderAmen('amenEdit', selIds);
            // Правила
            populateRuleSelect();
            renderRules(a.rules || [], a.id);
            // Рядом
            populateNearbySelect();
            renderNearby(a.nearby || [], a.id);
            // Фото
            const ph = document.getElementById('editPhotos');
            if (a.gallery?.length) {
                ph.innerHTML = a.gallery.map(p => `
                <div class="pi ${p.isMain ? 'main' : ''}" id="ph-${p.id}">
                    <img src="${imgPath(p.imagePath)}" alt="" loading="lazy">
                    <button type="button" class="pi-del" onclick="delPhoto(${a.id},${p.id})">
                        <i class="fa-solid fa-xmark"></i>
                    </button>
                    <div class="pi-ft">
                        ${p.isMain
                    ? '<span style="color:var(--a);">★ Главное</span>'
                    : `<button type="button" onclick="setMain(${a.id},${p.id})"
                                style="background:none;border:1px solid var(--bl);border-radius:4px;
                                padding:2px 7px;font-size:10px;cursor:pointer;color:var(--tm);font-family:inherit;">
                                Сделать главным</button>`}
                    </div>
                </div>`).join('');
            } else {
                ph.innerHTML = '<p style="color:var(--tm);font-size:.85rem;">Нет фотографий</p>';
            }
            switchEditTab('amenities', document.querySelector('.etab'));
        } catch (err) { toast(err.message, 'er'); closeEdit(); }
    }

    function closeEdit() { document.getElementById('editModal').classList.remove('open'); }

    async function saveEdit(e) {
        e.preventDefault();
        const id = document.getElementById('eId').value;
        try {
            await api(`/apartments/${id}`, {
                method: 'PUT',
                body: JSON.stringify({
                    title:         document.getElementById('eTitle').value.trim(),
                    address:       document.getElementById('eAddr').value.trim(),
                    pricePerNight: parseFloat(document.getElementById('ePrice').value),
                    guests:        parseInt(document.getElementById('eGuests').value),
                    area:          parseFloat(document.getElementById('eArea').value),
                    roomsCount:    parseInt(document.getElementById('eRooms').value),
                    description:   document.getElementById('eDesc').value.trim(),
                    isActive:      true,
                    isDeleted:     false
                })
            });

            await api(`/apartments/${id}/amenities`, {
                method: 'POST',
                body: JSON.stringify({ amenityIds: getChecked('amenEdit') })
            });

            const files = document.getElementById('eFiles').files;
            if (files.length) {
                const fd = new FormData();
                for (const f of files) fd.append('files', f);
                fd.append('mainPhotoIndex', -1);
                await fetch(`${API}/apartments/${id}/photos`, {
                    method: 'POST',
                    body: fd,
                    headers: { 'Authorization': `Bearer ${auth?.token}` }
                });
            }

            toast('Изменения сохранены!');
            closeEdit();
            loadGrid();
        } catch (err) { toast(err.message, 'er'); }
    }

    async function delPhoto(aptId, photoId) {
        if (!confirm('Удалить фото?')) return;
        try {
            await api(`/apartments/${aptId}/photos/${photoId}`, { method: 'DELETE' });
            document.getElementById(`ph-${photoId}`)?.remove();
            toast('Фото удалено');
        } catch (err) { toast(err.message, 'er'); }
    }

    async function setMain(aptId, photoId) {
        try {
            await api(`/apartments/${aptId}/photos/${photoId}/set-main`, { method: 'PATCH' });
            toast('Главное фото обновлено');
            openEdit(aptId);
        } catch (err) { toast(err.message, 'er'); }
    }

    document.getElementById('editModal').addEventListener('click', e => {
        if (e.target === e.currentTarget) closeEdit();
    });

    /* ── БРОНИРОВАНИЯ ────────────────────────────────────── */
    const sbadge = s => ({
        'Pending':   '<span class="badge by">Ожидание</span>',
        'Confirmed': '<span class="badge bg">Подтверждено</span>',
        'Cancelled': '<span class="badge br">Отменено</span>',
    }[s] ?? `<span class="badge bz">${s}</span>`);

    const fd = s => new Date(s).toLocaleDateString('ru-RU');

    async function loadBookings() {
        try {
            const [books, apts, clis] = await Promise.all([
                api('/bookings'),
                api('/apartments/all'),
                api('/clients')
            ]);

            // Заполняем select квартир в форме
            const sel = document.getElementById('bookApt');
            const cur = sel.value;
            sel.innerHTML = '<option value="">— выберите —</option>';
            apts.forEach(a => {
                const o = document.createElement('option');
                o.value = a.id; o.textContent = a.title;
                if (String(a.id) === cur) o.selected = true;
                sel.appendChild(o);
            });

            const am = Object.fromEntries(apts.map(a => [a.id, a.title]));
            const cm = Object.fromEntries(clis.map(c => [c.id, c.fullName]));
            const tb = document.getElementById('tbBook');

            if (!books.length) {
                tb.innerHTML = '<tr><td colspan="8"><div class="es"><i class="fa-solid fa-calendar"></i><p>Нет бронирований</p></div></td></tr>';
                return;
            }

            tb.innerHTML = books.map(b => `
            <tr>
                <td style="color:var(--tm);">#${b.id}</td>
                <td><strong>${am[b.apartmentId] ?? `#${b.apartmentId}`}</strong></td>
                <td>${cm[b.clientId] ?? `#${b.clientId}`}</td>
                <td>${fd(b.startDate)}</td>
                <td>${fd(b.endDate)}</td>
                <td style="color:var(--a);font-weight:700;">${b.totalPrice.toLocaleString('ru-RU')} ₽</td>
                <td>${sbadge(b.status)}</td>
                <td>
                    <div style="display:flex; gap:6px;">
                        ${b.status === 'Pending'
                ? `<button class="btn bs sm" onclick="confirmBook(${b.id})">
                                <i class="fa-solid fa-check"></i> Подтвердить
                               </button>`
                : ''}

                        ${b.status !== 'Cancelled'
                ? `<button class="btn bd sm" onclick="cancelBook(${b.id})">
                                ${b.status === 'Pending' ? 'Отклонить' : 'Отменить'}
                               </button>`
                : '—'}
                    </div>
                </td>
            </tr>`).join('');
        } catch (err) {
            document.getElementById('tbBook').innerHTML =
                `<tr><td colspan="8" style="color:var(--red);padding:16px;">${err.message}</td></tr>`;
        }
    }

    async function createBook(e) {
        e.preventDefault();
        const start = document.getElementById('bookIn').value;
        const end   = document.getElementById('bookOut').value;
        const cid   = document.getElementById('bookCli').value;
        if (!cid)       { toast('Выберите клиента из поиска', 'er'); return; }
        if (start >= end) { toast('Дата выезда должна быть позже заезда', 'er'); return; }
        try {
            await api('/bookings', {
                method: 'POST',
                body: JSON.stringify({
                    apartmentId: parseInt(document.getElementById('bookApt').value),
                    clientId: parseInt(cid),
                    startDate: start,
                    endDate: end
                })
            });
            toast('Бронирование создано!');
            document.getElementById('formBook').reset();
            clearCli();
            loadBookings();
        } catch (err) { toast(err.message, 'er'); }
    }

    async function confirmBook(id) {
        if (!confirm('Подтвердить бронирование?')) return;
        try {
            // Отправляем запрос на бэкенд (предполагаемый путь /confirm)
            await api(`/bookings/${id}/confirm`, { method: 'PATCH' });
            toast('Бронирование успешно подтверждено');
            loadBookings(); // Обновляем список
        } catch (err) {
            toast(err.message, 'er');
        }
    }

    async function cancelBook(id) {
        if (!confirm('Отменить бронирование?')) return;
        try {
            await api(`/bookings/${id}/cancel`, { method: 'PATCH' });
            toast('Отменено');
            loadBookings();
        } catch (err) { toast(err.message, 'er'); }
    }

    /* ── КЛИЕНТЫ ─────────────────────────────────────────── */
    async function loadClients() {
        try {
            const list = await api('/clients');
            const tb = document.getElementById('tbCli');
            if (!list.length) {
                tb.innerHTML = '<tr><td colspan="5"><div class="es"><i class="fa-solid fa-users"></i><p>Клиентов нет</p></div></td></tr>';
                return;
            }
            tb.innerHTML = list.map(c => `
            <tr>
                <td style="color:var(--tm);">#${c.id}</td>
                <td><strong>${c.fullName}</strong></td>
                <td><a href="tel:${c.phoneNumber}" style="color:var(--a);text-decoration:none;">${c.phoneNumber}</a></td>
                <td style="color:var(--tm);">${c.email || '—'}</td>
                <td><button class="btn bd sm" onclick="deleteCli(${c.id})"><i class="fa-solid fa-trash"></i></button></td>
            </tr>`).join('');
        } catch (err) {
            document.getElementById('tbCli').innerHTML =
                `<tr><td colspan="5" style="color:var(--red);padding:16px;">${err.message}</td></tr>`;
        }
    }

    async function createCli(e) {
        e.preventDefault();
        try {
            await api('/clients', {
                method: 'POST',
                body: JSON.stringify({
                    fullName:    document.getElementById('cName').value.trim(),
                    phoneNumber: document.getElementById('cPhone').value.trim(),
                    email:       document.getElementById('cEmail').value.trim()
                })
            });
            toast('Клиент добавлен!');
            document.getElementById('formCli').reset();
            loadClients();
        } catch (err) { toast(err.message, 'er'); }
    }

    async function deleteCli(id) {
        if (!confirm('Удалить клиента?')) return;
        try {
            await api(`/clients/${id}`, { method: 'DELETE' });
            toast('Клиент удалён');
            loadClients();
        } catch (err) { toast(err.message, 'er'); }
    }

    /* ── ПРАВИЛА ─────────────────────────────────────────────── */
    function renderRules(rules, aptId) {
        const el = document.getElementById('editRules');
        if (!rules.length) { el.innerHTML = '<span style="color:var(--tm);font-size:.85rem;">Нет правил</span>'; return; }
        el.innerHTML = rules.map(r => `
            <div style="display:flex;align-items:center;gap:8px;padding:8px 12px;
                        background:var(--bg3);border-radius:6px;border:1px solid var(--bl);">
                <i class="fa-solid ${r.icon}" style="color:var(--a);width:16px;text-align:center;"></i>
                <span style="flex:1;font-size:.875rem;">${r.text}</span>
                <button type="button" class="btn bd sm" onclick="deleteRule(${aptId},${r.id})">
                    <i class="fa-solid fa-xmark"></i>
                </button>
            </div>`).join('');
    }

    async function addRule() {
        const id     = document.getElementById('eId').value;
        const sel    = document.getElementById('ruleSelect');
        const ruleId = parseInt(sel?.value);
        if (!ruleId) { toast('Выберите правило', 'er'); return; }
        try {
            const a        = await api(`/apartments/${id}`);
            const current  = (a.rules || []).map(r => r.id);
            if (current.includes(ruleId)) { toast('Правило уже добавлено', 'er'); return; }
            await api(`/apartments/${id}/rules`, {
                method: 'POST',
                body: JSON.stringify({ Ids: [...current, ruleId] })
            });
            const updated = await api(`/apartments/${id}`);
            renderRules(updated.rules || [], parseInt(id));
            toast('Правило добавлено');
        } catch (err) { toast(err.message, 'er'); }
    }

    async function deleteRule(aptId, ruleId) {
        try {
            await api(`/apartments/${aptId}/rules/${ruleId}`, { method: 'DELETE' });
            const a = await api(`/apartments/${aptId}`);
            renderRules(a.rules || [], aptId);
            toast('Правило удалено');
        } catch (err) { toast(err.message, 'er'); }
    }

    /* ── РЯДОМ ───────────────────────────────────────────────── */
    function renderNearby(nearby, aptId) {
        const el = document.getElementById('editNearby');
        if (!nearby.length) { el.innerHTML = '<span style="color:var(--tm);font-size:.85rem;">Ничего не добавлено</span>'; return; }
        el.innerHTML = nearby.map(n => `
            <div style="display:flex;align-items:center;gap:8px;padding:8px 12px;
                        background:var(--bg3);border-radius:6px;border:1px solid var(--bl);">
                <i class="fa-solid ${n.icon}" style="color:var(--a);width:16px;text-align:center;"></i>
                <span style="flex:1;font-size:.875rem;">${n.name}</span>
                <span style="color:var(--tm);font-size:.8rem;">${n.distance}</span>
                <button type="button" class="btn bd sm" onclick="deleteNearby(${aptId},${n.id})">
                    <i class="fa-solid fa-xmark"></i>
                </button>
            </div>`).join('');
    }

    async function addNearby() {
        const id           = document.getElementById('eId').value;
        const sel          = document.getElementById('nearbySelect');
        const nearbyPlaceId = parseInt(sel?.value);
        const distance     = document.getElementById('nearbyDistance').value.trim();
        if (!nearbyPlaceId) { toast('Выберите место', 'er'); return; }
        if (!distance)      { toast('Укажите расстояние', 'er'); return; }
        try {
            await api(`/apartments/${id}/nearby`, {
                method: 'POST',
                body: JSON.stringify({ nearbyPlaceId, distance })
            });
            document.getElementById('nearbyDistance').value = '';
            const a = await api(`/apartments/${id}`);
            renderNearby(a.nearby || [], parseInt(id));
            toast('Место добавлено');
        } catch (err) { toast(err.message, 'er'); }
    }

    async function deleteNearby(aptId, nearbyPlaceId) {
        try {
            await api(`/apartments/${aptId}/nearby/${nearbyPlaceId}`, { method: 'DELETE' });
            const a = await api(`/apartments/${aptId}`);
            renderNearby(a.nearby || [], aptId);
            toast('Место удалено');
        } catch (err) { toast(err.message, 'er'); }
    }

    /* ── ПРАВИЛА (каталог) ───────────────────────────────── */
    async function loadRules() {
        try {
            const list = await api('/apartments/rules');
            const tb = document.getElementById('tbRules');
            if (!list.length) {
                tb.innerHTML = '<tr><td colspan="4"><div class="es"><i class="fa-solid fa-ban"></i><p>Правил нет</p></div></td></tr>';
                return;
            }
            tb.innerHTML = list.map(r => `
            <tr>
                <td style="color:var(--tm);">#${r.id}</td>
                <td><i class="fa-solid ${r.icon}" style="color:var(--a);"></i> <span style="color:var(--tm);font-size:.8rem;">${r.icon}</span></td>
                <td>${r.text}</td>
                <td><button class="btn bd sm" onclick="deleteRuleCatalog(${r.id})"><i class="fa-solid fa-trash"></i></button></td>
            </tr>`).join('');
        } catch (err) {
            document.getElementById('tbRules').innerHTML =
                `<tr><td colspan="4" style="color:var(--red);padding:16px;">${err.message}</td></tr>`;
        }
    }

    async function createRule(e) {
        e.preventDefault();
        const icon = document.getElementById('rNewIcon').value.trim();
        const text = document.getElementById('rNewText').value.trim();
        try {
            await api('/apartments/rules', { method: 'POST', body: JSON.stringify({ icon, text }) });
            toast('Правило добавлено');
            document.getElementById('formRule').reset();
            await loadRulesCatalog();
            loadRules();
        } catch (err) { toast(err.message, 'er'); }
    }

    async function deleteRuleCatalog(id) {
        if (!confirm('Удалить правило?')) return;
        try {
            await api(`/apartments/rules/${id}`, { method: 'DELETE' });
            toast('Правило удалено');
            loadRules();
        } catch (err) { toast(err.message, 'er'); }
    }

    /* ── РЯДОМ (каталог) ─────────────────────────────────── */
    async function loadNearby() {
        try {
            const list = await api('/apartments/nearby-places');
            const tb = document.getElementById('tbNearby');
            if (!list.length) {
                tb.innerHTML = '<tr><td colspan="5"><div class="es"><i class="fa-solid fa-map"></i><p>Мест нет</p></div></td></tr>';
                return;
            }
            tb.innerHTML = list.map(n => `
            <tr>
                <td style="color:var(--tm);">#${n.id}</td>
                <td><i class="fa-solid ${n.icon}" style="color:var(--a);"></i> <span style="color:var(--tm);font-size:.8rem;">${n.icon}</span></td>
                <td>${n.name}</td>
                <td style="color:var(--tm);">${n.distance}</td>
                <td><button class="btn bd sm" onclick="deleteNearbyCatalog(${n.id})"><i class="fa-solid fa-trash"></i></button></td>
            </tr>`).join('');
        } catch (err) {
            document.getElementById('tbNearby').innerHTML =
                `<tr><td colspan="5" style="color:var(--red);padding:16px;">${err.message}</td></tr>`;
        }
    }

    async function createNearbyPlace(e) {
        e.preventDefault();
        const icon = document.getElementById('nNewIcon').value.trim();
        const name = document.getElementById('nNewName').value.trim();
        try {
            await api('/apartments/nearby-places', { method: 'POST', body: JSON.stringify({ icon, name }) });
            toast('Место добавлено');
            document.getElementById('formNearby').reset();
            await loadNearbyPlacesCatalog();
            loadNearby();
        } catch (err) { toast(err.message, 'er'); }
    }

    async function deleteNearbyCatalog(id) {
        if (!confirm('Удалить место?')) return;
        try {
            await api(`/apartments/nearby-places/${id}`, { method: 'DELETE' });
            toast('Место удалено');
            loadNearby();
        } catch (err) { toast(err.message, 'er'); }
    }
    /* ── ВИКИ (каталог) ─────────────────────────────────── */
    async function createWikiPlace(e) {
        e.preventDefault();
        try {
            const body = {
                name:          document.getElementById('wNewName').value.trim(),
                description:   document.getElementById('wNewDescription').value.trim(),
                type:          document.getElementById('wikiPlaceTypeSelect').value,
                nearbyPlaceId: parseInt(document.getElementById('wikiPlaceNearbySelect').value) || null
            };
            const wp = await api('/wiki-places', { method: 'POST', body: JSON.stringify(body) });

            // Фото
            const file = document.getElementById('wNewPhoto').files[0];
            if (file) {
                const fd = new FormData();
                fd.append('file', file);
                await fetch(`${API}/wiki-places/${wp.id}/photo`, {
                    method: 'POST',
                    body: fd,
                    headers: { 'Authorization': `Bearer ${auth?.token}` }
                });
            }

            toast('Место добавлено');
            document.getElementById('formWikiPlace').reset();
            loadWikiPlaces();
        } catch (err) { toast(err.message, 'er'); }
    }

    async function loadWikiPlaces() {
        try {
            const list = await api('/wiki-places');
            const tb = document.getElementById('tbWikiPlaces');
            if (!list.length) {
                tb.innerHTML = '<tr><td colspan="4"><div class="es"><i class="fa-solid fa-map"></i><p>Мест нет</p></div></td></tr>';
                return;
            }
            tb.innerHTML = list.map(w => `
            <tr>
                <td style="color:var(--tm);">#${w.id}</td>
                <td>${w.name}</td>
                <td style="color:var(--tm);">${w.type}</td>
                <td style="display:flex;gap:6px;">
                    <button class="btn bi sm" onclick="openWikiEdit(${w.id})"><i class="fa-solid fa-pen"></i></button>
                    <button class="btn bd sm" onclick="deleteWikiPlace(${w.id})"><i class="fa-solid fa-trash"></i></button>
                </td>
            </tr>`).join('');
        } catch (err) {
            document.getElementById('tbWikiPlaces').innerHTML =
                `<tr><td colspan="4" style="color:var(--red);padding:16px;">${err.message}</td></tr>`;
        }
    }

    async function openWikiEdit(id) {
        const wp = await api(`/wiki-places/${id}`);
        document.getElementById('wpId').value          = wp.id;
        document.getElementById('wpName').value        = wp.name;
        document.getElementById('wpDescription').value = wp.description;
        document.getElementById('wpType').value        = wp.type;
        const photo = document.getElementById('wpCurrentPhoto');
        if (wp.photoPath) {
            photo.src   = imgPath(wp.photoPath);
            photo.style.display = 'block';
        } else {
            photo.style.display = 'none';
        }
        document.getElementById('wpNearby').innerHTML = nearbyPlacesCatalog.length
            ? nearbyPlacesCatalog.map(n =>
                `<option value="${n.id}" ${n.id === wp.nearbyPlaceId ? 'selected' : ''}>
                    ${n.name}
                </option>`).join('')
            : '<option value="" disabled>Нет мест в каталоге</option>';
        document.getElementById('wikiEditModal').classList.add('open');
    }

    function closeWikiEdit() {
        document.getElementById('wikiEditModal').classList.remove('open');
        document.getElementById('wpPhoto').value = '';
    }

    async function saveWikiEdit(e) {
        e.preventDefault();
        const id = document.getElementById('wpId').value;
        try {
            await api(`/wiki-places/${id}`, {
                method: 'PUT',
                body: JSON.stringify({
                    name:          document.getElementById('wpName').value.trim(),
                    description:   document.getElementById('wpDescription').value.trim(),
                    type:          document.getElementById('wpType').value,
                    nearbyPlaceId: parseInt(document.getElementById('wpNearby').value) || null
                })
            });

            const file = document.getElementById('wpPhoto').files[0];
            if (file) {
                const fd = new FormData();
                fd.append('file', file);
                await fetch(`${API}/wiki-places/${id}/photo`, {
                    method: 'POST', body: fd,
                    headers: { 'Authorization': `Bearer ${auth?.token}` }
                });
            }

            toast('Место обновлено');
            closeWikiEdit();
            loadWikiPlaces();
        } catch (err) { toast(err.message, 'er'); }
    }

    async function deleteWikiPhoto(id) {
        if (!confirm('Удалить фото?')) return;
        try {
            await api(`/wiki-places/${id}/photo`, { method: 'DELETE' });
            document.getElementById('wpCurrentPhoto').style.display = 'none';
            toast('Фото удалено');
        } catch (err) { toast(err.message, 'er'); }
    }

    async function deleteWikiPlace(id) {
        if (!confirm('Удалить место?')) return;
        try {
            await api(`/wiki-places/${id}`, { method: 'DELETE' });
            toast('Место удалено');
            loadWikiPlaces();
        } catch (err) { toast(err.message, 'er'); }
    }

    function switchEditTab(pane, btn) {
        document.querySelectorAll('.etab-pane').forEach(el => el.classList.remove('active'));
        document.querySelectorAll('.etab').forEach(el => el.classList.remove('active'));
        document.getElementById(`epane-${pane}`).classList.add('active');
        btn.classList.add('active');
    }

    function populateWikiNearbySelect() {
        const sel = document.getElementById('wikiPlaceNearbySelect');
        if (!sel) return;
        sel.innerHTML = '<option value="">— не указывать —</option>' +
            (nearbyPlacesCatalog.length
                ? nearbyPlacesCatalog.map(n =>
                    `<option value="${n.id}">${n.name}</option>`).join('')
                : '');
    }

    /* ── INIT ────────────────────────────────────────────── */
    (async () => {
        // ── Защита страницы: проверяем токен и роль ──────────
        const raw = localStorage.getItem('sutkivmv_auth');
        try { auth = raw ? JSON.parse(raw) : null; } catch { auth = null; }

        if (!auth?.token || auth?.user?.role !== 'Admin') {
            document.body.innerHTML = `
            <div style="display:flex;flex-direction:column;align-items:center;justify-content:center;
                        min-height:100vh;background:#0f1117;color:#e8eaf0;font-family:'Manrope',sans-serif;gap:16px;">
                <i class="fa-solid fa-lock" style="font-size:3rem;color:#ef4444;"></i>
                <h2 style="margin:0;font-size:1.4rem;">Доступ запрещён</h2>
                <p style="color:#7a8399;margin:0;">Эта страница доступна только администратору.</p>
                <a href="frontend/login.html"
                   style="margin-top:8px;padding:10px 24px;background:#f59e0b;color:#000;
                          border-radius:8px;text-decoration:none;font-weight:700;">
                    Войти
                </a>
            </div>`;
            return;
        }

        // ── Добавляем токен во все API-запросы ────────────────
        const _origApi = window.api;
        window.api = async function(url, opts = {}) {
            const headers = { 'Content-Type': 'application/json', ...(opts.headers ?? {}) };
            headers['Authorization'] = `Bearer ${auth.token}`;
            const res = await fetch(API + url, { ...opts, headers });
            if (res.status === 401 || res.status === 403) {
                localStorage.removeItem('sutkivmv_auth');
                location.href = 'frontend/login.html';
                return;
            }
            if (!res.ok) {
                const b = await res.json().catch(() => ({}));
                throw new Error(b.message || `HTTP ${res.status}`);
            }
            if (res.status === 204) return null;
            return res.json();
        };

        // ── Показываем имя админа в сайдбаре ─────────────────
        const foot = document.querySelector('.sb-foot');
        if (foot) {
            const name = auth.user.fullName || auth.user.email || 'Admin';
            const initials = name.split(' ').map(w => w[0]).slice(0, 2).join('').toUpperCase() || 'A';
            const logoutLink = document.createElement('a');
            logoutLink.href = '#';
            logoutLink.innerHTML = `<i class="fa-solid fa-user-shield"></i> ${name}`;
            logoutLink.style.cssText = 'color:var(--a);cursor:default;pointer-events:none;';
            foot.insertBefore(logoutLink, foot.firstChild);

            const logoutBtn = document.createElement('a');
            logoutBtn.href = '#';
            logoutBtn.innerHTML = '<i class="fa-solid fa-arrow-right-from-bracket"></i> Выйти';
            logoutBtn.onclick = (e) => {
                e.preventDefault();
                localStorage.removeItem('sutkivmv_auth');
                location.href = 'frontend/login.html';
            };
            foot.insertBefore(logoutBtn, foot.querySelector('a[href]'));
        }

        await loadAmenities();
        await loadRulesCatalog();
        await loadNearbyPlacesCatalog();
        await loadWikiPlaces();
        populateWikiNearbySelect();
        renderAmen('amenAdd', []);
    })();