"use strict";

document.addEventListener('DOMContentLoaded', function () {
    const defaultAdminLang = 'en'; // Default language for admin
    let currentAdminLang = localStorage.getItem('adminLanguage') || defaultAdminLang;

    // --- Language Switching ---
    const langButtons = document.querySelectorAll('.admin-lang-switch-btn');
    const currentLangDisplay = document.getElementById('adminCurrentLang');

    function applyAdminTranslation(lang) {
        console.log(`Applying admin translation: ${lang}`);
        document.documentElement.lang = lang;
        document.documentElement.dir = (lang === 'ar') ? 'rtl' : 'ltr';
        document.body.classList.toggle('rtl', lang === 'ar');

        // Update dropdown text
        if (currentLangDisplay) {
            currentLangDisplay.textContent = lang === 'ar' ? 'العربية' : 'English';
        }

        // Update elements with data-key
        document.querySelectorAll('[data-key]').forEach(el => {
            const key = el.getAttribute('data-key'); // Not really used here, but good practice
            const text = el.getAttribute(`data-${lang}`);

            if (text) {
                // Update based on element type
                if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') {
                    if (el.type !== 'file' && el.type !== 'hidden') { // Don't set placeholder for file/hidden inputs
                        el.placeholder = text;
                    }
                } else if (el.tagName === 'BUTTON' || el.tagName === 'A' || el.tagName === 'SPAN' || el.tagName === 'LABEL' || el.tagName === 'H1' || el.tagName === 'H4' || el.tagName === 'H5' || el.tagName === 'P' || el.tagName === 'SMALL' || el.tagName === 'TH' || el.tagName === 'DT') {
                    // Check if it's a button/link with only an icon inside, don't change text
                    const onlyIcon = el.children.length === 1 && el.children[0].tagName === 'I';
                    if (!onlyIcon || el.children.length === 0) { // Update text if no children or children are not just an icon
                        // Find the innermost text node or span to update if possible
                        let textElement = el.querySelector('span[data-key]') || el; // Prioritize inner span if exists
                        if (textElement.childNodes.length > 0 && textElement.childNodes[0].nodeType === Node.TEXT_NODE && !onlyIcon) {
                            textElement.childNodes[0].nodeValue = text + (textElement.childNodes[0].nodeValue.endsWith('?') ? '?' : ''); // Keep trailing punctuation
                        } else if (!onlyIcon) {
                            textElement.textContent = text;
                        }
                    }
                    // Special case for buttons/links: update title attribute too if exists
                    if ((el.tagName === 'BUTTON' || el.tagName === 'A') && el.hasAttribute('title')) {
                        el.title = text;
                    }

                } else if (el.tagName === 'DIV' && el.classList.contains('alert')) {
                    // Simple update for alert text, assuming text is direct child node
                    if (el.childNodes.length > 0 && el.childNodes[0].nodeType === Node.TEXT_NODE) {
                        el.childNodes[0].nodeValue = text;
                    }
                }
            }
        });

        // Specific adjustments if needed
        adjustLayoutForLang(lang);
    }

    function adjustLayoutForLang(lang) {
        // Example: Adjust margins for icons if needed (might be handled by CSS better)
        document.querySelectorAll('.me-1, .me-2').forEach(el => {
            el.classList.toggle('ms-1', lang === 'ar');
            el.classList.toggle('ms-2', lang === 'ar');
            el.classList.toggle('me-1', lang !== 'ar');
            el.classList.toggle('me-2', lang !== 'ar');
        });
        document.querySelectorAll('.ms-1, .ms-2').forEach(el => {
            el.classList.toggle('me-1', lang === 'ar');
            el.classList.toggle('me-2', lang === 'ar');
            el.classList.toggle('ms-1', lang !== 'ar');
            el.classList.toggle('ms-2', lang !== 'ar');
        });
    }

    // Attach event listeners to language buttons
    langButtons.forEach(button => {
        button.addEventListener('click', function () {
            const selectedLang = this.getAttribute('data-lang');
            if (selectedLang !== currentAdminLang) {
                currentAdminLang = selectedLang;
                localStorage.setItem('adminLanguage', currentAdminLang);
                applyAdminTranslation(currentAdminLang);
                // Optionally close dropdown if inside one
                var dropdown = bootstrap.Dropdown.getInstance(document.getElementById('navbarLangDropdown'));
                if (dropdown) dropdown.hide();
            }
        });
    });

    // Apply initial translation on page load
    applyAdminTranslation(currentAdminLang);

    // --- Other Admin JS (e.g., confirmations, previews) ---

    // Example: Preview selected image before upload
    const imageFileInput = document.getElementById('ImageFile'); // Ensure IDs match in Create/Edit views
    const imagePreview = document.getElementById('imagePreview'); // Add an <img> tag with this ID in your forms

    if (imageFileInput && imagePreview) {
        imageFileInput.addEventListener('change', function (event) {
            const file = event.target.files[0];
            if (file) {
                const reader = new FileReader();
                reader.onload = function (e) {
                    imagePreview.src = e.target.result;
                    imagePreview.style.display = 'block'; // Make it visible
                }
                reader.readAsDataURL(file);
            } else {
                imagePreview.src = '#'; // Clear preview
                imagePreview.style.display = 'none'; // Hide it
            }
        });
    }

});

