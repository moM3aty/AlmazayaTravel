"use strict";

document.addEventListener('DOMContentLoaded', function () {
    const defaultAdminLang = 'en';
    let currentAdminLang = localStorage.getItem('adminLanguage') || defaultAdminLang;

    const langButtons = document.querySelectorAll('.admin-lang-switch-btn');
    const currentLangDisplay = document.getElementById('adminCurrentLang');

    function applyAdminTranslation(lang) {
        console.log(`Applying admin translation: ${lang}`);
        document.documentElement.lang = lang;
        document.documentElement.dir = (lang === 'ar') ? 'rtl' : 'ltr';
        document.body.classList.toggle('rtl', lang === 'ar');

        if (currentLangDisplay) {
            currentLangDisplay.textContent = lang === 'ar' ? 'العربية' : 'English';
        }

        document.querySelectorAll('[data-key]').forEach(el => {
            const key = el.getAttribute('data-key');
            const text = el.getAttribute(`data-${lang}`);

            if (text) {
                if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') {
                    if (el.type !== 'file' && el.type !== 'hidden') {
                        el.placeholder = text;
                    }
                } else if (el.tagName === 'BUTTON' || el.tagName === 'A' || el.tagName === 'SPAN' || el.tagName === 'LABEL' || el.tagName === 'H1' || el.tagName === 'H4' || el.tagName === 'H5' || el.tagName === 'P' || el.tagName === 'SMALL' || el.tagName === 'TH' || el.tagName === 'DT') {
                    const onlyIcon = el.children.length === 1 && el.children[0].tagName === 'I';
                    if (!onlyIcon || el.children.length === 0) {
                        let textElement = el.querySelector('span[data-key]') || el;
                        if (textElement.childNodes.length > 0 && textElement.childNodes[0].nodeType === Node.TEXT_NODE && !onlyIcon) {
                            textElement.childNodes[0].nodeValue = text + (textElement.childNodes[0].nodeValue.endsWith('?') ? '?' : '');
                        } else if (!onlyIcon) {
                            textElement.textContent = text;
                        }
                    }
                    if ((el.tagName === 'BUTTON' || el.tagName === 'A') && el.hasAttribute('title')) {
                        el.title = text;
                    }

                } else if (el.tagName === 'DIV' && el.classList.contains('alert')) {
                    if (el.childNodes.length > 0 && el.childNodes[0].nodeType === Node.TEXT_NODE) {
                        el.childNodes[0].nodeValue = text;
                    }
                }
            }
        });

        adjustLayoutForLang(lang);
    }

    function adjustLayoutForLang(lang) {
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

    langButtons.forEach(button => {
        button.addEventListener('click', function () {
            const selectedLang = this.getAttribute('data-lang');
            if (selectedLang !== currentAdminLang) {
                currentAdminLang = selectedLang;
                localStorage.setItem('adminLanguage', currentAdminLang);
                applyAdminTranslation(currentAdminLang);
                var dropdown = bootstrap.Dropdown.getInstance(document.getElementById('navbarLangDropdown'));
                if (dropdown) dropdown.hide();
            }
        });
    });

    applyAdminTranslation(currentAdminLang);

    const imageFileInput = document.getElementById('ImageFile');
    const imagePreview = document.getElementById('imagePreview');

    if (imageFileInput && imagePreview) {
        imageFileInput.addEventListener('change', function (event) {
            const file = event.target.files[0];
            if (file) {
                const reader = new FileReader();
                reader.onload = function (e) {
                    imagePreview.src = e.target.result;
                    imagePreview.style.display = 'block';
                }
                reader.readAsDataURL(file);
            } else {
                imagePreview.src = '#';
                imagePreview.style.display = 'none';
            }
        });
    }

});
