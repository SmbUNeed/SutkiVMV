/* ── Config ────────────────────────────────────────────── */
const API_URL = 'https://sutkivmv.ru/api';
function imgUrl(path) {
    if (!path) return '';
    if (path.startsWith('http')) return path;
    return `https://sutkivmv.ru${path}`;
}

/* ── Theme toggle ──────────────────────────────────────── */
document.getElementById('themeToggle').addEventListener('click', function() {
    const html = document.documentElement;
    const next = html.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
    html.setAttribute('data-theme', next);
    localStorage.setItem('sutkivmv_theme', next);
});

let wikiPlaces = [];
let filteredWikiPlaces = [];
const grid = document.getElementById('wikiGrid');

const WIKI_TYPES = {
    sight:     { icon: 'fa-camera',         label: 'Достопримечательность' },
    nature:    { icon: 'fa-tree',            label: 'Природа' },
    resort:    { icon: 'fa-water',           label: 'Курорт' },
    food:      { icon: 'fa-utensils',        label: 'Питание' },
    shopping:  { icon: 'fa-basket-shopping', label: 'Торговля' },
    transport: { icon: 'fa-train',           label: 'Транспорт' },
    other:     { icon: 'fa-location-dot',    label: 'Прочее' },
};

/* ── Observer — объявляем ДО init ──────────────────────── */
const observer = new IntersectionObserver((entries) => {
    entries.forEach(e => {
        if (e.isIntersecting) {
            e.target.style.opacity = '1';
            e.target.style.transform = 'translateY(0)';
            observer.unobserve(e.target);
        }
    });
}, { threshold: 0.08 });

async function apiGetWikiPlaces() {
    try {
        const res = await fetch(`${API_URL}/wiki-places`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return await res.json();
    } catch (err) {
        console.error('Ошибка загрузки мест:', err);
        return [];
    }
}

function renderGrid(data) {
    grid.innerHTML = '';
    if (!data.length) {
        grid.innerHTML = `
            <div class="wiki-no-results">
                <i class="fa-solid fa-face-sad-cry"></i>
                <p>Нет мест в этой категории.<br>Попробуйте другой фильтр.</p>
            </div>`;
        return;
    }
    data.forEach(wp => {
        const type = wp.type ?? 'other';
        const t    = WIKI_TYPES[type] ?? WIKI_TYPES.other;
        const bg   = wp.photoPath
            ? `style="background-image:url('${imgUrl(wp.photoPath)}')"`
            : '';

        grid.insertAdjacentHTML('beforeend', `
            <div class="wiki-card cat-${type}" data-cat="${type}" onclick="openWikiPlaceModal(${wp.id})">
                <div class="wiki-card-thumb">
                    <div class="wiki-card-thumb-bg" ${bg}></div>
                    <div class="wiki-card-cat-tag">
                        <i class="fa-solid ${t.icon}"></i> ${t.label}
                    </div>
                </div>
                <div class="wiki-card-body">
                    <div class="wiki-card-title">${wp.name}</div>
                    <div class="wiki-card-desc">${wp.description}</div>
                </div>
            </div>`);
    });

    // Observer после рендера
    grid.querySelectorAll('.wiki-card').forEach((card, i) => {
        card.style.opacity = '0';
        card.style.transform = 'translateY(20px)';
        card.style.transition = `opacity 0.4s ease ${i * 0.05}s, transform 0.4s ease ${i * 0.05}s, border-color 0.2s ease, box-shadow 0.2s ease`;
        observer.observe(card);
    });
}

async function openWikiPlaceModal(id) {
    const wp = await fetch(`${API_URL}/wiki-places/${id}`).then(r => r.json());
    const t  = WIKI_TYPES[wp.type] ?? WIKI_TYPES.other;

    const photo = document.getElementById('wikiModalPhoto');
    photo.style.backgroundImage = wp.photoPath ? `url('${imgUrl(wp.photoPath)}')` : 'none';

    document.getElementById('wikiModalCat').innerHTML  = `<i class="fa-solid ${t.icon}"></i> ${t.label}`;
    document.getElementById('wikiModalTitle').textContent = wp.name;
    document.getElementById('wikiModalDesc').textContent  = wp.description;

    const aptsEl = document.getElementById('wikiModalApts');
    aptsEl.innerHTML = '';
    if (wp.nearbyApartments?.length) {
        wp.nearbyApartments.forEach(apt => {
            aptsEl.insertAdjacentHTML('beforeend', `
                <div class="wiki-apt-card" onclick="window.location.href='index.html?apt=${apt.id}'">
                    <img src="${imgUrl(apt.mainImage)}" alt="${apt.title}">
                    <div class="wiki-apt-info">
                        <div class="wiki-apt-name">${apt.title}</div>
                        <div class="wiki-apt-price">от ${apt.pricePerNight.toLocaleString('ru')} ₽</div>
                    </div>
                    <i class="fa-solid fa-chevron-right wiki-apt-arrow"></i>
                </div>`);
        });
    } else {
        aptsEl.innerHTML = '';
    }

    document.getElementById('wikiModal').classList.add('open');
    document.body.style.overflow = 'hidden';
}

function closeWikiModal() {
    document.getElementById('wikiModal').classList.remove('open');
    document.body.style.overflow = '';
}

function filterWiki(cat) {
    document.querySelectorAll('.wiki-cat-btn').forEach(btn => {
        btn.classList.toggle('active', btn.dataset.cat === cat);
    });

    filteredWikiPlaces = cat === 'all'
        ? wikiPlaces
        : wikiPlaces.filter(wp => wp.type === cat);

    renderGrid(filteredWikiPlaces);
}

async function init() {
    wikiPlaces = await apiGetWikiPlaces();
    filteredWikiPlaces = wikiPlaces;
    renderGrid(wikiPlaces);

    const params = new URLSearchParams(window.location.search);
    const placeId = params.get('place');
    if (placeId) {
        openWikiPlaceModal(parseInt(placeId));
        history.replaceState(null, '', window.location.pathname);
    }
}

/* ── Climate rows ──────────────────────────────────────── */
const climate = [
    { m: 'Январь',   hi: 4,  emoji: '❄️' },
    { m: 'Февраль',  hi: 6,  emoji: '❄️' },
    { m: 'Март',     hi: 12, emoji: '🌧️' },
    { m: 'Апрель',   hi: 18, emoji: '🌤️' },
    { m: 'Май',      hi: 23, emoji: '☀️' },
    { m: 'Июнь',     hi: 27, emoji: '☀️' },
    { m: 'Июль',     hi: 30, emoji: '🌡️' },
    { m: 'Август',   hi: 29, emoji: '🌡️' },
    { m: 'Сентябрь', hi: 24, emoji: '🍂' },
    { m: 'Октябрь',  hi: 17, emoji: '🍂' },
    { m: 'Ноябрь',   hi: 10, emoji: '🌧️' },
    { m: 'Декабрь',  hi: 6,  emoji: '❄️' },
];
const maxHi = Math.max(...climate.map(c => c.hi));
const rows  = document.getElementById('climateRows');
rows.innerHTML = climate.map(c => {
    const pct = Math.round((c.hi / maxHi) * 100);
    return `
    <div style="display:grid;grid-template-columns:70px 1fr 32px;align-items:center;gap:8px;">
        <span style="font-size:0.76rem;font-weight:700;color:var(--ink-2);">${c.emoji} ${c.m.slice(0,3)}</span>
        <div style="background:var(--paper-3);border-radius:99px;height:6px;overflow:hidden;">
            <div style="width:${pct}%;height:100%;background:var(--primary);border-radius:99px;transition:width 0.6s ease;"></div>
        </div>
        <span style="font-size:0.76rem;font-weight:800;color:var(--primary);text-align:right;">${c.hi}°</span>
    </div>`;
}).join('');

init();