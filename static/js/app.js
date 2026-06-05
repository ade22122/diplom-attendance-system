document.addEventListener("DOMContentLoaded", () => {
    const registrationForm = document.querySelector("[data-registration-form]");
    if (registrationForm) {
        const password = registrationForm.querySelector("#id_password1");
        const passwordConfirm = registrationForm.querySelector("#id_password2");
        const passwordMessage = registrationForm.querySelector("[data-password-match-error]");
        const email = registrationForm.querySelector("#id_email");
        const emailMessage = registrationForm.querySelector("[data-email-availability]");
        const emailCheckUrl = registrationForm.dataset.emailCheckUrl;
        let emailTimer;
        let emailAbort;

        const setLiveMessage = (element, text, isError = false) => {
            if (!element) {
                return;
            }
            element.textContent = text;
            element.classList.toggle("field-error", Boolean(text && isError));
            element.classList.toggle("field-ok", Boolean(text && !isError));
        };

        const validatePasswordMatch = () => {
            if (!password || !passwordConfirm) {
                return true;
            }
            const mismatch = passwordConfirm.value && password.value !== passwordConfirm.value;
            passwordConfirm.setCustomValidity(mismatch ? "Пароли не совпадают." : "");
            passwordConfirm.setAttribute("aria-invalid", mismatch ? "true" : "false");
            if (mismatch) {
                setLiveMessage(passwordMessage, "Пароли не совпадают.", true);
            } else if (password.value && passwordConfirm.value) {
                setLiveMessage(passwordMessage, "Пароли совпадают.");
            } else {
                setLiveMessage(passwordMessage, "");
            }
            return !mismatch;
        };

        const checkEmail = () => {
            if (!email || !emailCheckUrl) {
                return;
            }
            clearTimeout(emailTimer);
            email.setCustomValidity("");
            email.setAttribute("aria-invalid", "false");

            const value = email.value.trim();
            if (!value) {
                setLiveMessage(emailMessage, "");
                return;
            }
            if (email.validity.typeMismatch) {
                setLiveMessage(emailMessage, "Введите корректный email.", true);
                return;
            }

            setLiveMessage(emailMessage, "Проверяем email...");
            emailTimer = window.setTimeout(async () => {
                if (emailAbort) {
                    emailAbort.abort();
                }
                emailAbort = new AbortController();
                try {
                    const url = new URL(emailCheckUrl, window.location.origin);
                    url.searchParams.set("email", value);
                    const response = await fetch(url, {
                        credentials: "same-origin",
                        signal: emailAbort.signal,
                    });
                    if (!response.ok) {
                        throw new Error("Email check failed");
                    }
                    const data = await response.json();
                    const currentValue = email.value.trim();
                    if (currentValue !== value) {
                        return;
                    }
                    email.setCustomValidity(data.available ? "" : data.message);
                    email.setAttribute("aria-invalid", data.available ? "false" : "true");
                    setLiveMessage(emailMessage, data.message, !data.available);
                } catch (error) {
                    if (error.name !== "AbortError") {
                        setLiveMessage(emailMessage, "Email проверится при отправке формы.", true);
                    }
                }
            }, 350);
        };

        password?.addEventListener("input", validatePasswordMatch);
        passwordConfirm?.addEventListener("input", validatePasswordMatch);
        email?.addEventListener("input", checkEmail);
        email?.addEventListener("blur", checkEmail);

        registrationForm.addEventListener("submit", (event) => {
            if (!validatePasswordMatch()) {
                event.preventDefault();
                passwordConfirm.reportValidity();
            }
        });
    }

    document.querySelectorAll("[data-tabs]").forEach((tabsRoot) => {
        const buttons = tabsRoot.querySelectorAll("[data-tab-target]");
        const panels = tabsRoot.querySelectorAll("[data-tab-panel]");

        buttons.forEach((button) => {
            button.addEventListener("click", () => {
                const target = button.dataset.tabTarget;

                buttons.forEach((item) => item.classList.toggle("active", item === button));
                panels.forEach((panel) => {
                    panel.classList.toggle("active", panel.dataset.tabPanel === target);
                });
            });
        });
    });

    document.querySelectorAll("[data-flip-card]").forEach((card) => {
        card.addEventListener("click", () => {
            card.classList.toggle("flipped");
        });
    });

    document.querySelectorAll("[data-task-list]").forEach((list) => {
        const storageKey = `tasks:${list.dataset.taskList}`;
        const saved = JSON.parse(localStorage.getItem(storageKey) || "{}");

        list.querySelectorAll("[data-task-id]").forEach((checkbox) => {
            checkbox.checked = Boolean(saved[checkbox.dataset.taskId]);
            checkbox.closest("label").classList.toggle("done", checkbox.checked);

            checkbox.addEventListener("change", () => {
                saved[checkbox.dataset.taskId] = checkbox.checked;
                checkbox.closest("label").classList.toggle("done", checkbox.checked);
                localStorage.setItem(storageKey, JSON.stringify(saved));
            });
        });
    });
});
