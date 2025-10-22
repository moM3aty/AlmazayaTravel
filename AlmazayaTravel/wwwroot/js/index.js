// --- Navbar Scroll Handling ---
function handleNavbarScroll() {
    const navbar = document.getElementById("main-nav");
    const scrollIndicator = document.querySelector(".scroll-indicator"); // Get progress bar

    if (window.scrollY > 50) {
        navbar.classList.add("scrolled");
    } else {
        navbar.classList.remove("scrolled");
    }
    updateScrollProgress(); // Update progress bar on scroll
}

window.addEventListener("scroll", handleNavbarScroll);

// --- Translation Handling ---
document.addEventListener("DOMContentLoaded", function () {
    const translateBtn = document.getElementById("translateBtn");
    const translateBtnText = translateBtn ? translateBtn.querySelector("span") : null;
    let currentLang = localStorage.getItem("language") || "en"; // Default to English

    // Set initial language state
    localStorage.setItem("language", currentLang);
    applyTranslation(currentLang);
    // Initialize Swiper after initial translation
    initSwiper(currentLang);

    // Apply necessary classes/attributes on load
    handleNavbarScroll(); // Set initial navbar state
    createScrollProgress(); // Create the scroll indicator
    updateScrollProgress(); // Set initial progress
    animateCounters(); // Initialize counters animation
    AOS.init({ // Initialize AOS animations
        duration: 800, // Slightly faster duration
        once: true, // Animate elements only once
        offset: 50, // Trigger animation a bit earlier
    });


    if (translateBtn) {
        translateBtn.addEventListener("click", function () {
            currentLang = currentLang === "en" ? "ar" : "en";
            localStorage.setItem("language", currentLang);
            applyTranslation(currentLang);

            // --- Close Navbar on Language Change (Mobile) ---
            const navbarCollapse = document.querySelector(".navbar-collapse");
            const isNavbarCollapsed = navbarCollapse && navbarCollapse.classList.contains("show");
            if (isNavbarCollapsed) {
                new bootstrap.Collapse(navbarCollapse).hide();
            }
            // Ensure navbar stays visually scrolled after language change if page isn't at top
            if (window.scrollY > 50) {
                const navbar = document.getElementById("main-nav");
                if (navbar) navbar.classList.add("scrolled");
            }
        });
    }
});


function applyTranslation(lang) {
    // Update text content based on data attributes
    document.querySelectorAll("[data-en][data-ar]").forEach(el => {
        let textValue = el.dataset[lang];
        if (textValue !== undefined) {
            if (el.tagName === "INPUT" || el.tagName === "TEXTAREA") {
                el.placeholder = textValue;
            } else if (el.tagName === "OPTION" && el.value === "") {
                // Handle placeholder options in select dropdowns
                el.textContent = textValue;
            } else if (el.tagName === "BUTTON" && el.querySelector('span')) {
                // Handle buttons with icons and text spans (like translate button)
                const span = el.querySelector('span');
                if (span && span.dataset[lang] !== undefined) {
                    span.textContent = span.dataset[lang];
                } else {
                    // Fallback if only button has data attrs
                    el.textContent = textValue; // This might remove the icon, adjust if needed
                }
            }
            else {
                // Direct text replacement for most elements (p, h1-h6, a, span, etc.)
                // Avoid replacing content if it includes child elements (like icons within buttons/headings)
                let hasChildElements = el.children.length > 0;
                let onlyTextNodes = Array.from(el.childNodes).every(node => node.nodeType === Node.TEXT_NODE || (node.nodeType === Node.ELEMENT_NODE && node.tagName === 'SPAN' && node.classList.contains('me-2'))); // Allow specific spans like in translate btn

                if (!hasChildElements || onlyTextNodes || el.classList.contains('counter-label')) { // Allow direct replace for simple cases or specific classes
                    el.textContent = textValue;
                } else {
                    // Attempt to replace only the main text node if complex element
                    for (let i = 0; i < el.childNodes.length; i++) {
                        if (el.childNodes[i].nodeType === Node.TEXT_NODE && el.childNodes[i].textContent.trim()) {
                            el.childNodes[i].textContent = textValue;
                            break; // Assume first text node is the target
                        }
                    }
                }

            }
        }
    });

    // Update HTML lang and dir attributes
    document.documentElement.lang = lang;
    document.documentElement.dir = lang === "ar" ? "rtl" : "ltr";
    document.body.classList.toggle("rtl", lang === "ar");

    // Re-initialize Swiper for the new direction
    initSwiper(lang);

    // Update error messages if present
    document.querySelectorAll(".error-msg").forEach(el => {
        if (el.dataset[lang]) {
            el.textContent = el.dataset[lang];
        }
    });

    // Re-run AOS refresh might be needed if layout shifts significantly
    if (typeof AOS !== 'undefined') {
        AOS.refresh();
    }
}

// --- Scroll Progress Indicator ---
function createScrollProgress() {
    // Check if it already exists
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
        const scrollPercentage = totalScroll > 0 ? (currentScroll / totalScroll) : 0; // Avoid division by zero
        progressBar.style.transform = `scaleX(${scrollPercentage})`;
    }
}

// --- Counter Animation ---
function animateCounters() {
    const counters = document.querySelectorAll(".counter");
    if (counters.length === 0) return; // Exit if no counters

    const observer = new IntersectionObserver((entries, observer) => { // Add observer to arguments
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                const counter = entry.target;
                const target = parseInt(counter.getAttribute("data-target"), 10);
                let current = 0;
                // Calculate increment dynamically for smoother animation over ~1 second
                const duration = 1000; // ms
                const stepTime = 20; // ms per step
                const totalSteps = duration / stepTime;
                const increment = target / totalSteps;


                const updateCounter = () => {
                    current += increment;
                    if (current < target) {
                        counter.textContent = Math.ceil(current);
                        setTimeout(updateCounter, stepTime);
                    } else {
                        counter.textContent = target; // Ensure final value is exact
                    }
                };

                updateCounter();
                observer.unobserve(counter); // Stop observing once animated
            }
        });
    }, { threshold: 0.5 }); // Trigger when 50% visible

    counters.forEach(counter => observer.observe(counter));
}


// --- Contact Form Handling (WhatsApp Integration) ---
document.getElementById("contactForm")?.addEventListener("submit", function (e) {
    e.preventDefault();

    const form = e.target;
    let isValid = true;
    let errors = {};

    // --- Clear previous errors ---
    form.querySelectorAll(".error-msg").forEach(el => el.remove());
    form.querySelectorAll(".is-invalid").forEach(el => el.classList.remove("is-invalid"));

    // --- Get form values ---
    const firstName = form.firstName.value.trim();
    const lastName = form.lastName.value.trim();
    const email = form.email.value.trim();
    const phone = form.phone.value.trim();
    const destination = form.destination.value.trim();
    const travelDate = form.travelDate.value.trim();
    const travelers = form.travelers.value.trim();
    const message = form.message.value.trim();

    // --- Basic Validation Rules ---
    const nameRegex = /^[A-Za-z\u0600-\u06FF\s.'-]+$/; // Allow more characters in names
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    const phoneRegex = /^\+?\d{7,15}$/; // Allow '+' and range of digits

    // --- Validation Checks ---
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
        // Optional: Check if date is in the past
        const today = new Date();
        const selectedDate = new Date(travelDate);
        today.setHours(0, 0, 0, 0); // Reset time for comparison
        if (selectedDate < today) {
            errors.travelDate = { en: "Travel date cannot be in the past", ar: "لا يمكن أن يكون تاريخ السفر في الماضي" };
            isValid = false;
        }
    }

    if (!travelers) {
        errors.travelers = { en: "Please select the number of travelers", ar: "الرجاء اختيار عدد المسافرين" };
        isValid = false;
    }

    // --- Display errors ---
    const currentLang = localStorage.getItem("language") || "en";
    for (const key in errors) {
        const field = form.querySelector(`#${key}`);
        if (field) {
            field.classList.add("is-invalid"); // Add Bootstrap invalid class
            const error = document.createElement("small");
            error.className = "error-msg text-danger d-block mt-1";
            error.dataset.en = errors[key].en; // Store both translations
            error.dataset.ar = errors[key].ar;
            error.textContent = errors[key][currentLang]; // Display current language error
            // Insert after the input/select element
            field.parentNode.insertBefore(error, field.nextSibling);

            // Remove error on input/change
            field.addEventListener("input", handleFieldErrorClear, { once: true });
            field.addEventListener("change", handleFieldErrorClear, { once: true });
        }
    }

    // --- Submit if valid ---
    if (isValid) {
        const text =
            `*New Travel Inquiry:*\n\n` +
            `*Name:* ${firstName} ${lastName}\n` +
            `*Email:* ${email}\n` +
            `*Phone:* ${phone}\n` +
            `*Destination:* ${form.querySelector('#destination option:checked').textContent.replace('Choose Destination', '').replace('اختر الوجهة', '').trim()}\n` + // Get selected option text
            `*Travel Date:* ${travelDate}\n` +
            `*Travelers:* ${form.querySelector('#travelers option:checked').textContent.replace('Number of Travelers', '').replace('عدد المسافرين', '').trim()}\n` + // Get selected option text
            (message ? `*Message:* ${message}\n` : ''); // Only include message if provided

        const whatsappNumber = "966567638260"; // Replace with your actual WhatsApp number
        const url = `https://wa.me/${whatsappNumber}?text=${encodeURIComponent(text)}`;
        window.open(url, "_blank");

        // Optionally reset form and clear errors after successful "submission"
        form.reset();
        form.querySelectorAll(".is-invalid").forEach(el => el.classList.remove("is-invalid"));
        form.querySelectorAll(".error-msg").forEach(el => el.remove());

        // Optional: Show a success message (e.g., using a Bootstrap alert or a custom modal)
        // alert("Your inquiry has been opened in WhatsApp!"); // Replace alert later
    }
});

// Helper function to remove validation error styling and message
function handleFieldErrorClear(event) {
    const field = event.target;
    field.classList.remove("is-invalid");
    const errorMsg = field.parentNode.querySelector(".error-msg");
    if (errorMsg) {
        errorMsg.remove();
    }
}


// --- Back to Top Button ---
const backToTopBtn = document.getElementById("backToTop");
if (backToTopBtn) {
    window.addEventListener("scroll", () => {
        if (window.scrollY > 300) { // Show after scrolling down a bit more
            backToTopBtn.classList.add("show");
        } else {
            backToTopBtn.classList.remove("show");
        }
    });

    backToTopBtn.addEventListener("click", () => {
        window.scrollTo({ top: 0, behavior: "smooth" });
    });
}

// --- Navbar Collapse Handling ---
const navbar = document.getElementById("main-nav");
const navbarToggler = document.querySelector(".navbar-toggler");
const navbarCollapse = document.querySelector(".navbar-collapse");

// Add scrolled class when toggler is clicked (for mobile background)
if (navbarToggler && navbar) {
    navbarToggler.addEventListener("click", () => {
        // Only add scrolled class if navbar isn't already scrolled and it's expanded
        if (!navbar.classList.contains('scrolled') && navbarCollapse && !navbarCollapse.classList.contains('show')) {
            navbar.classList.add("scrolled"); // Add immediately for background
        } else if (!navbar.classList.contains('scrolled') && navbarCollapse && navbarCollapse.classList.contains('show')) {
            // If closing and not scrolled past threshold, remove scrolled class
            if (window.scrollY <= 50) {
                setTimeout(() => { // Delay slightly to allow collapse animation
                    navbar.classList.remove("scrolled");
                }, 350); // Adjust timeout based on collapse animation duration
            }
        }
    });
}


// Close navbar collapse when a nav link is clicked
document.querySelectorAll(".nav-link").forEach(link => {
    link.addEventListener("click", () => {
        if (navbarCollapse && navbarCollapse.classList.contains("show")) {
            new bootstrap.Collapse(navbarCollapse).hide();
        }
        // Keep navbar scrolled if page is not at top
        if (window.scrollY > 50 && navbar) {
            navbar.classList.add("scrolled");
        }
    });
});


// --- Swiper Initialization ---
let swiperInstance; // Use a global variable to manage the instance

function initSwiper(lang) {
    if (swiperInstance) {
        swiperInstance.destroy(true, true); // Destroy previous instance if exists
    }

    // Determine direction based on language
    const direction = lang === 'ar' ? 'rtl' : 'ltr';

    swiperInstance = new Swiper(".mySwiper", {
        loop: true,
        autoplay: {
            delay: 3000, // Slightly longer delay
            disableOnInteraction: false,
        },
        // Removed pagination option
        navigation: {
            nextEl: ".swiper-button-next",
            prevEl: ".swiper-button-prev",
        },
        slidesPerView: 1, // Default slides per view
        spaceBetween: 15, // Default space
        breakpoints: {
            // Small devices (landscape phones, 576px and up)
            576: {
                slidesPerView: 2,
                spaceBetween: 20
            },
            // Medium devices (tablets, 768px and up)
            768: {
                slidesPerView: 2,
                spaceBetween: 25
            },
            // Large devices (desktops, 992px and up)
            992: {
                slidesPerView: 3,
                spaceBetween: 30
            },
            // Extra large devices (large desktops, 1200px and up)
            1200: {
                slidesPerView: 3, // Keep 3 or increase if desired
                spaceBetween: 30
            }
        },
        direction: 'horizontal', // Explicitly horizontal
        // Key addition for RTL support:
        rtl: lang === 'ar', // Set based on language
        // effect: 'slide', // Default effect
        grabCursor: true, // Add grab cursor effect
    });
}
