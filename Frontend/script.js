const API_URL = "http://localhost:5048/api";

let apartments = [];
let filteredApartments = [];
let currentApartment = null;
let currentDate = new Date();
let currentAptBookedDates = [];
let selectedCheckIn = null;
let selectedCheckOut = null;

// =====================
//  API
// =====================

function showSkeletons(count = 3) {
    grid.innerHTML = Array(count).fill(`
        <div class="apartment-card skeleton">
            <div class="card-image skeleton-box"></div>
            <div class="card-content">
                <div class="skeleton-line"></div>
                <div class="skeleton-line short"></div>
            </div>
        </div>
    `).join("");
}

async function apiGetApartments() {
    try {
        const res = await fetch(`${API_URL}/apartments/detailed`);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return await res.json();
    } catch (err) {
        console.error("Ошибка загрузки квартир:", err);
        return [];
    }
}

async function apiCreateClient(fullName, phoneNumber) {
    const res = await fetch(`${API_URL}/clients`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ fullName, phoneNumber, email: "" })
    });
    if (!res.ok) throw new Error("Не удалось создать клиента");
    return res.json();
}

async function apiCreateBooking(apartmentId, clientId, startDate, endDate) {
    const res = await fetch(`${API_URL}/bookings`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ apartmentId, clientId, startDate, endDate })
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.message || "Ошибка бронирования");
    return data;
}

// =====================
//  HELPERS
// =====================

// Превращает массив бронирований [{startDate, endDate, status}]
// в плоский массив строк занятых дат "YYYY-MM-DD"
function getBookedDates(bookings) {
    const dates = [];
    for (const b of bookings) {
        if (b.status === "Cancelled") continue;
        const start = new Date(b.startDate);
        const end   = new Date(b.endDate);
        for (let d = new Date(start); d < end; d.setDate(d.getDate() + 1)) {
            dates.push(d.toISOString().slice(0, 10));
        }
    }
    return dates;
}

function toDateStr(date) {
    return date.toISOString().slice(0, 10);
}

// =====================
//  GRID / CARDS
// =====================

const grid = document.getElementById("apartmentsGrid");

function renderGrid(data) {
    grid.innerHTML = "";
    if (data.length === 0) {
        grid.innerHTML = "<p class='no-results'>К сожалению, подходящих квартир не найдено. Попробуйте изменить фильтры.</p>";
        return;
    }

    data.forEach(apt => {
        const amenitiesText = apt.amenities.length ? apt.amenities.join(", ") : "—";
        const imgSrc = apt.mainImage ?? "https://placehold.co/600x400?text=Нет+фото";

        grid.insertAdjacentHTML("beforeend", `
            <div class="apartment-card" onclick="openModal(${apt.id})">
                <div class="card-image">
                    <img src="${imgSrc}" alt="${apt.title}">
                    <div class="price-badge">от ${apt.pricePerNight.toLocaleString("ru-RU")} ₽</div>
                </div>
                <div class="card-content">
                    <h3>${apt.title}</h3>
                    <p class="specs">
                        <i class="fa-solid fa-user-group"></i> до ${apt.guests} гостей
                        •
                        <i class="fa-solid fa-ruler-combined"></i> ${apt.area} м²
                    </p>
                    <p class="amenities-preview">${amenitiesText}</p>
                    <div class="card-footer">
                        <span class="status available">Свободна</span>
                        <button class="btn-primary">Посмотреть</button>
                    </div>
                </div>
            </div>
        `);
    });
}

// =====================
//  FILTERS
// =====================

function applyFilters() {
    const guests   = parseInt(document.getElementById("guests").value)   || 0;
    const priceMin = parseInt(document.getElementById("priceMin").value) || 0;
    const priceMax = parseInt(document.getElementById("priceMax").value) || Infinity;
    const amenity  = document.getElementById("amenities").value;
    const sortVal  = document.getElementById("sortSelect").value;

    filteredApartments = apartments.filter(apt =>
        apt.guests         >= guests   &&
        apt.pricePerNight  >= priceMin &&
        apt.pricePerNight  <= priceMax &&
        (amenity === "" || apt.amenities.includes(amenity))
    );

    if (sortVal === "price_asc")  filteredApartments.sort((a, b) => a.pricePerNight - b.pricePerNight);
    if (sortVal === "price_desc") filteredApartments.sort((a, b) => b.pricePerNight - a.pricePerNight);
    if (sortVal === "area_desc")  filteredApartments.sort((a, b) => b.area - a.area);

    renderGrid(filteredApartments);
}

["guests", "priceMin", "priceMax"].forEach(id =>
    document.getElementById(id).addEventListener("input", applyFilters)
);
["amenities", "sortSelect"].forEach(id =>
    document.getElementById(id).addEventListener("change", applyFilters)
);

// =====================
//  MODAL
// =====================

function openModal(apartmentId) {
    const apt = apartments.find(a => a.id === apartmentId);
    if (!apt) return;
    currentApartment = apt;

    document.getElementById("modalTitle").innerText = apt.title;
    document.querySelector(".modal-specs").innerText =
        `Вместимость: ${apt.guests} гостей | Площадь: ${apt.area} м²`;
    document.querySelector(".modal-description p").innerText = apt.description;
    document.querySelector(".main-img").src =
        apt.mainImage ?? "https://placehold.co/600x400?text=Нет+фото";
    document.querySelector(".booking-price strong").innerText =
        `${apt.pricePerNight.toLocaleString("ru-RU")} ₽`;

    // Сбрасываем выбор дат
    selectedCheckIn  = null;
    selectedCheckOut = null;
    currentAptBookedDates = getBookedDates(apt.bookings);
    updateBookingSummary();
    renderCalendar();

    document.getElementById("apartmentModal").classList.add("active");
    document.body.style.overflow = "hidden";
}

function closeModal() {
    document.getElementById("apartmentModal").classList.remove("active");
    document.body.style.overflow = "auto";
    currentApartment = null;
}

document.getElementById("apartmentModal").addEventListener("click", e => {
    if (e.target === e.currentTarget) closeModal();
});

// =====================
//  CALENDAR
// =====================

const MONTH_NAMES = [
    "Январь","Февраль","Март","Апрель","Май","Июнь",
    "Июль","Август","Сентябрь","Октябрь","Ноябрь","Декабрь"
];

function renderCalendar() {
    const year     = currentDate.getFullYear();
    const month    = currentDate.getMonth();
    const firstDay = new Date(year, month, 1).getDay();
    const lastDay  = new Date(year, month + 1, 0).getDate();
    const startDay = firstDay === 0 ? 6 : firstDay - 1; // Неделя с понедельника
    const todayStr = toDateStr(new Date());

    let html = `
        <div class="calendar-header">
            <button onclick="changeMonth(-1)"><i class="fa-solid fa-chevron-left"></i></button>
            <span>${MONTH_NAMES[month]} ${year}</span>
            <button onclick="changeMonth(1)"><i class="fa-solid fa-chevron-right"></i></button>
        </div>
        <div class="calendar-grid">
            ${["Пн","Вт","Ср","Чт","Пт","Сб","Вс"].map(d =>
                `<div class="calendar-day-name">${d}</div>`
            ).join("")}
            ${"<div class='calendar-day empty'></div>".repeat(startDay)}
    `;

    for (let i = 1; i <= lastDay; i++) {
        const dateStr  = `${year}-${String(month + 1).padStart(2,"0")}-${String(i).padStart(2,"0")}`;
        const isPast   = dateStr < todayStr;
        const isBooked = currentAptBookedDates.includes(dateStr);

        // Подсветка диапазона
        const isStart  = dateStr === selectedCheckIn;
        const isEnd    = dateStr === selectedCheckOut;
        const inRange  = selectedCheckIn && selectedCheckOut
            && dateStr > selectedCheckIn && dateStr < selectedCheckOut;

        let cls = "calendar-day";
        if (isPast || isBooked) cls += " booked";
        else if (isStart)       cls += " check-in";
        else if (isEnd)         cls += " check-out";
        else if (inRange)       cls += " in-range";

        const clickAttr = (!isPast && !isBooked) ? `onclick="selectDate('${dateStr}')"` : "";
        html += `<div class="${cls}" ${clickAttr}>${i}</div>`;
    }

    html += "</div>";
    document.getElementById("bookingCalendar").innerHTML = html;
}

function changeMonth(step) {
    currentDate.setMonth(currentDate.getMonth() + step);
    renderCalendar();
}

function selectDate(dateStr) {
    // Если нет начала или уже выбран диапазон — начинаем заново
    if (!selectedCheckIn || (selectedCheckIn && selectedCheckOut)) {
        selectedCheckIn  = dateStr;
        selectedCheckOut = null;

    } else if (dateStr <= selectedCheckIn) {
        // Кликнули раньше или на ту же дату — сдвигаем начало
        selectedCheckIn = dateStr;

    } else {
        // Проверяем, нет ли занятых дат внутри диапазона
        let hasConflict = false;
        const start = new Date(selectedCheckIn);
        const end   = new Date(dateStr);
        for (let d = new Date(start); d < end; d.setDate(d.getDate() + 1)) {
            if (currentAptBookedDates.includes(toDateStr(d))) {
                hasConflict = true;
                break;
            }
        }

        if (hasConflict) {
            alert("В выбранном диапазоне есть занятые даты. Пожалуйста, выберите другой период.");
            selectedCheckIn  = dateStr;
            selectedCheckOut = null;
        } else {
            selectedCheckOut = dateStr;
        }
    }

    renderCalendar();
    updateBookingSummary();
}

function updateBookingSummary() {
    const summaryEl = document.getElementById("bookingSummary");
    if (!summaryEl) return;

    if (selectedCheckIn && selectedCheckOut && currentApartment) {
        const nights = (new Date(selectedCheckOut) - new Date(selectedCheckIn)) / 86_400_000;
        const total  = nights * currentApartment.pricePerNight;
        summaryEl.innerHTML = `
            <span>${nights} ${nights === 1 ? "ночь" : nights < 5 ? "ночи" : "ночей"}</span>
            <strong>${total.toLocaleString("ru-RU")} ₽</strong>
        `;
        summaryEl.style.display = "flex";
    } else {
        summaryEl.style.display = "none";
    }
}

// =====================
//  BOOKING SUBMIT
// =====================

document.getElementById("btnBook").addEventListener("click", async () => {
    if (!selectedCheckIn || !selectedCheckOut) {
        alert("Пожалуйста, выберите даты заезда и выезда.");
        return;
    }

    const name  = document.getElementById("clientName").value.trim();
    const phone = document.getElementById("clientPhone").value.trim();

    if (!name || !phone) {
        alert("Пожалуйста, укажите имя и телефон.");
        return;
    }

    const btn = document.getElementById("btnBook");
    btn.disabled = true;
    btn.textContent = "Оформляем...";

    try {
        const client = await apiCreateClient(name, phone);
        await apiCreateBooking(
            currentApartment.id,
            client.id,
            selectedCheckIn,
            selectedCheckOut
        );

        alert("Бронирование успешно оформлено! Мы свяжемся с вами для подтверждения.");
        closeModal();

        // Перезагружаем квартиры, чтобы отразить новые занятые даты
        apartments = await apiGetApartments();
        filteredApartments = [...apartments];
        applyFilters();

    } catch (err) {
        alert(`Ошибка: ${err.message}`);
    } finally {
        btn.disabled = false;
        btn.textContent = "Забронировать";
    }
});

// =====================
//  INIT
// =====================

async function init() {
    showSkeletons();
    apartments = await apiGetApartments();
    filteredApartments = [...apartments];
    renderGrid(filteredApartments);
}

init();
