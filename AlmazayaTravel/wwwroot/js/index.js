function handleNavbarScroll() {
    const navbar = document.getElementById("main-nav");
    const scrollIndicator = document.querySelector(".scroll-indicator");

    if (window.scrollY > 50) {
        navbar.classList.add("scrolled");
    } else {
        navbar.classList.remove("scrolled");
    }
    updateScrollProgress();
}

window.addEventListener("scroll", handleNavbarScroll);

document.addEventListener("DOMContentLoaded", function () {
    const translateBtn = document.getElementById("translateBtn");
    const translateBtnText = translateBtn ? translateBtn.querySelector("span") : null;
    let currentLang = localStorage.getItem("language") || "en";

    localStorage.setItem("language", currentLang);
    applyTranslation(currentLang);
    initSwiper(currentLang);

    handleNavbarScroll();
    createScrollProgress();
    updateScrollProgress();
    animateCounters();
    AOS.init({
        duration: 800,
        once: true,
        offset: 50,
    });


    if (translateBtn) {
        translateBtn.addEventListener("click", function () {
            currentLang = currentLang === "en" ? "ar" : "en";
            localStorage.setItem("language", currentLang);
            applyTranslation(currentLang);

            const navbarCollapse = document.querySelector(".navbar-collapse");
            const isNavbarCollapsed = navbarCollapse && navbarCollapse.classList.contains("show");
            if (isNavbarCollapsed) {
                new bootstrap.Collapse(navbarCollapse).hide();
            }
            if (window.scrollY > 50) {
                const navbar = document.getElementById("main-nav");
                if (navbar) navbar.classList.add("scrolled");
            }
        });
    }
});


function applyTranslation(lang) {
    document.querySelectorAll("[data-en][data-ar]").forEach(el => {
        let textValue = el.dataset[lang];
        if (textValue !== undefined) {
            if (el.tagName === "INPUT" || el.tagName === "TEXTAREA") {
                el.placeholder = textValue;
            } else if (el.tagName === "OPTION" && el.value === "") {
                el.textContent = textValue;
            } else if (el.tagName === "BUTTON" && el.querySelector('span')) {
                const span = el.querySelector('span');
                if (span && span.dataset[lang] !== undefined) {
                    span.textContent = span.dataset[lang];
                } else {
                    el.textContent = textValue;
                }
            }
            else {
                let hasChildElements = el.children.length > 0;
                let onlyTextNodes = Array.from(el.childNodes).every(node => node.nodeType === Node.TEXT_NODE || (node.nodeType === Node.ELEMENT_NODE && node.tagName === 'SPAN' && node.classList.contains('me-2')));

                if (!hasChildElements || onlyTextNodes || el.classList.contains('counter-label')) {
                    el.textContent = textValue;
                } else {
                    for (let i = 0; i < el.childNodes.length; i++) {
                        if (el.childNodes[i].nodeType === Node.TEXT_NODE && el.childNodes[i].textContent.trim()) {
                            el.childNodes[i].textContent = textValue;
                            break;
                        }
                    }
                }

            }
        }
    });

    document.documentElement.lang = lang;
    document.documentElement.dir = lang === "ar" ? "rtl" : "ltr";
    document.body.classList.toggle("rtl", lang === "ar");

    initSwiper(lang);

    document.querySelectorAll(".error-msg").forEach(el => {
        if (el.dataset[lang]) {
            el.textContent = el.dataset[lang];
        }
    });

    if (typeof AOS !== 'undefined') {
        AOS.refresh();
    }
}

function createScrollProgress() {
    if (!document.querySelector(".scroll-indicator")) {
        const progressBar = document.createElement("div");
        progressBar.className = "scroll-indicator";
        document.body.appendChild(progressBar);
    }
}

function updateScrollProgress() {
    const progressBar = document.querySelector(".scroll-indicator");
    if (progressBar) {
        const totalScroll = document.documentElement.scrollHeight - window.innerHeight;
        const currentScroll = window.scrollY;
        const scrollPercentage = totalScroll > 0 ? (currentScroll / totalScroll) : 0;
        progressBar.style.transform = `scaleX(${scrollPercentage})`;
    }
}

function animateCounters() {
    const counters = document.querySelectorAll(".counter");
    if (counters.length === 0) return;

    const observer = new IntersectionObserver((entries, observer) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const counter = entry.target;
                const target = parseInt(counter.getAttribute("data-target"), 10);
                let current = 0;
                const duration = 1000;
                const stepTime = 20;
                const totalSteps = duration / stepTime;
                const increment = target / totalSteps;

                const updateCounter = () => {
                    current += increment;
                    if (current < target) {
                        counter.textContent = Math.ceil(current);
                        setTimeout(updateCounter, stepTime);
                    } else {
                        counter.textContent = target;
                    }
                };

                updateCounter();
                observer.unobserve(counter);
            }
        });
    }, { threshold: 0.5 });

    counters.forEach(counter => observer.observe(counter));
}

document.getElementById("contactForm")?.addEventListener("submit", function (e) {
    e.preventDefault();

    const form = e.target;
    let isValid = true;
    let errors = {};

    form.querySelectorAll(".error-msg").forEach(el => el.remove());
    form.querySelectorAll(".is-invalid").forEach(el => el.classList.remove("is-invalid"));

    const firstName = form.firstName.value.trim();
    const lastName = form.lastName.value.trim();
    const email = form.email.value.trim();
    const phone = form.phone.value.trim();
    const destination = form.destination.value.trim();
    const travelDate = form.travelDate.value.trim();
    const travelers = form.travelers.value.trim();
    const message = form.message.value.trim();

    const nameRegex = /^[A-Za-z\u0600-\u06FF\s.'-]+$/;
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    const phoneRegex = /^\+?\d{7,15}$/;

    if (!firstName) {
        errors.firstName = { en: "First name is required", ar: "الاسم الأول مطلوب" };
        isValid = false;
    } else if (!nameRegex.test(firstName)) {
        errors.firstName = { en: "Please enter a valid first name", ar: "الرجاء إدخال اسم أول صالح" };
        isValid = false;
    }

    if (!lastName) {
        errors.lastName = { en: "Last name is required", ar: "اسم العائلة مطلوب" };
        isValid = false;
    } else if (!nameRegex.test(lastName)) {
        errors.lastName = { en: "Please enter a valid last name", ar: "الرجاء إدخال اسم عائلة صالح" };
        isValid = false;
    }

    if (!email) {
        errors.email = { en: "Email is required", ar: "البريد الإلكتروني مطلوب" };
        isValid = false;
    } else if (!emailRegex.test(email)) {
        errors.email = { en: "Please enter a valid email address", ar: "الرجاء إدخال بريد إلكتروني صالح" };
        isValid = false;
    }

    if (!phone) {
        errors.phone = { en: "Phone number is required", ar: "رقم الهاتف مطلوب" };
        isValid = false;
    } else if (!phoneRegex.test(phone)) {
        errors.phone = { en: "Please enter a valid phone number (7-15 digits, optional +)", ar: "الرجاء إدخال رقم هاتف صالح (7-15 رقمًا، اختياريًا +)" };
        isValid = false;
    }

    if (!destination) {
        errors.destination = { en: "Please choose a destination", ar: "الرجاء اختيار الوجهة" };
        isValid = false;
    }

    if (!travelDate) {
        errors.travelDate = { en: "Please select a travel date", ar: "الرجاء اختيار تاريخ السفر" };
        isValid = false;
    } else {
        const today = new Date();
        const selectedDate = new Date(travelDate);
        today.setHours(0, 0, 0, 0);
        if (selectedDate < today) {
            errors.travelDate = { en: "Travel date cannot be in the past", ar: "لا يمكن أن يكون تاريخ السفر في الماضي" };
            isValid = false;
        }
    }

    if (!travelers) {
        errors.travelers = { en: "Please select the number of travelers", ar: "الرجاء اختيار عدد المسافرين" };
        isValid = false;
    }

    const currentLang = localStorage.getItem("language") || "en";
    for (const key in errors) {
        const field = form.querySelector(`#${key}`);
        if (field) {
            field.classList.add("is-invalid");
            const error = document.createElement("small");
            error.className = "error-msg text-danger d-block mt-1";
            error.dataset.en = errors[key].en;
            error.dataset.ar = errors[key].ar;
            error.textContent = errors[key][currentLang];
            field.parentNode.insertBefore(error, field.nextSibling);

            field.addEventListener("input", handleFieldErrorClear, { once: true });
            field.addEventListener("change", handleFieldErrorClear, { once: true });
        }
    }

    if (isValid) {
        const text =
            `*New Travel Inquiry:*\n\n` +
            `*Name:* ${firstName} ${lastName}\n` +
            `*Email:* ${email}\n` +
            `*Phone:* ${phone}\n` +
            `*Destination:* ${form.querySelector('#destination option:checked').textContent.replace('Choose Destination', '').replace('اختر الوجهة', '').trim()}\n` +
            `*Travel Date:* ${travelDate}\n` +
            `*Travelers:* ${form.querySelector('#travelers option:checked').textContent.replace('Number of Travelers', '').replace('عدد المسافرين', '').trim()}\n` +
            (message ? `*Message:* ${message}\n` : '');

        const whatsappNumber = "966567638260";
        const url = `https://wa.me/${whatsappNumber}?text=${encodeURIComponent(text)}`;
        window.open(url, "_blank");

        form.reset();
        form.querySelectorAll(".is-invalid").forEach(el => el.classList.remove("is-invalid"));
        form.querySelectorAll(".error-msg").forEach(el => el.remove());
    }
});

function handleFieldErrorClear(event) {
    const field = event.target;
    field.classList.remove("is-invalid");
    const errorMsg = field.parentNode.querySelector(".error-msg");
    if (errorMsg) {
        errorMsg.remove();
    }
}

const backToTopBtn = document.getElementById("backToTop");
if (backToTopBtn) {
    window.addEventListener("scroll", () => {
        if (window.scrollY > 300) {
            backToTopBtn.classList.add("show");
        } else {
            backToTopBtn.classList.remove("show");
        }
    });

    backToTopBtn.addEventListener("click", () => {
        window.scrollTo({ top: 0, behavior: "smooth" });
    });
}

const navbar = document.getElementById("main-nav");
const navbarToggler = document.querySelector(".navbar-toggler");
const navbarCollapse = document.querySelector(".navbar-collapse");

if (navbarToggler && navbar) {
    navbarToggler.addEventListener("click", () => {
        if (!navbar.classList.contains('scrolled') && navbarCollapse && !navbarCollapse.classList.contains('show')) {
            navbar.classList.add("scrolled");
        } else if (!navbar.classList.contains('scrolled') && navbarCollapse && navbarCollapse.classList.contains('show')) {
            if (window.scrollY <= 50) {
                setTimeout(() => {
                    navbar.classList.remove("scrolled");
                }, 350);
            }
        }
    });
}

document.querySelectorAll(".nav-link").forEach(link => {
    link.addEventListener("click", () => {
        if (navbarCollapse && navbarCollapse.classList.contains("show")) {
            new bootstrap.Collapse(navbarCollapse).hide();
        }
        if (window.scrollY > 50 && navbar) {
            navbar.classList.add("scrolled");
        }
    });
});

let swiperInstance;

function initSwiper(lang) {
    if (swiperInstance) {
        swiperInstance.destroy(true, true);
    }

    const direction = lang === 'ar' ? 'rtl' : 'ltr';

    swiperInstance = new Swiper(".mySwiper", {
        loop: true,
        autoplay: {
            delay: 3000,
            disableOnInteraction: false,
        },
        navigation: {
            nextEl: ".swiper-button-next",
            prevEl: ".swiper-button-prev",
        },
        slidesPerView: 1,
        spaceBetween: 15,
        breakpoints: {
            576: {
                slidesPerView: 2,
                spaceBetween: 20
            },
            768: {
                slidesPerView: 2,
                spaceBetween: 25
            },
            992: {
                slidesPerView: 3,
                spaceBetween: 30
            },
            1200: {
                slidesPerView: 3,
                spaceBetween: 30
            }
        },
        direction: 'horizontal',
        rtl: lang === 'ar',
        grabCursor: true,
    });
}
