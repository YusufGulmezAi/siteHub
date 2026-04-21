// ==========================================================================
// sitehub-auth.js
// Blazor Server circuit'i taray\u0131c\u0131 cookie'lerini do\u011frudan set edemez.
// Bu yard\u0131mc\u0131, /auth/login ve /auth/logout endpoint'lerini fetch ile \u00e7a\u011f\u0131r\u0131r.
// Set-Cookie header'\u0131 cevapta d\u00f6ner → taray\u0131c\u0131 otomatik saklar.
// ==========================================================================

window.sitehubAuth = {

    /**
     * POST /auth/login
     * @param {string} input - TCKN / e-posta / telefon / VKN
     * @param {string} password - kullan\u0131c\u0131 parolas\u0131
     * @returns {Promise<{success: boolean, sessionId?: string, message?: string}>}
     */
    async login(input, password) {
        try {
            const response = await fetch("/auth/login", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "Accept": "application/json"
                },
                credentials: "same-origin",
                body: JSON.stringify({ input, password })
            });

            if (response.ok) {
                const data = await response.json();
                return {
                    success: true,
                    sessionId: data.sessionId,
                    requiresTwoFactor: data.requiresTwoFactor ?? false
                };
            }

            // Backend'den hata kodu gelmi\u015f olabilir (401, 400)
            let message = null;
            try {
                const error = await response.json();
                message = error.message || null;
            } catch {
                // JSON parse edilemiyorsa statusText'i kullan
                message = response.statusText;
            }

            return { success: false, message };

        } catch (err) {
            console.error("Login fetch hatas\u0131:", err);
            return { success: false, message: "Sunucuya ula\u015f\u0131lam\u0131yor." };
        }
    },

    /**
     * POST /auth/logout
     * @returns {Promise<boolean>}
     */
    async logout() {
        try {
            const response = await fetch("/auth/logout", {
                method: "POST",
                credentials: "same-origin"
            });
            return response.ok;
        } catch (err) {
            console.error("Logout fetch hatas\u0131:", err);
            return false;
        }
    },

    /**
     * POST /auth/request-password-reset
     * @param {string} input - TCKN / e-posta / telefon / VKN
     * @param {string} channel - 'Email' veya 'Sms'
     * @returns {Promise<{success: boolean, message: string}>}
     */
    async requestPasswordReset(input, channel) {
        try {
            const response = await fetch("/auth/request-password-reset", {
                method: "POST",
                headers: { "Content-Type": "application/json", "Accept": "application/json" },
                credentials: "same-origin",
                body: JSON.stringify({ input, channel })
            });
            const data = await response.json();
            return { success: data.success ?? false, message: data.message ?? "" };
        } catch (err) {
            console.error("PasswordReset request hatas\u0131:", err);
            return { success: false, message: "Sunucuya ula\u015f\u0131lam\u0131yor." };
        }
    },

    /**
     * POST /auth/reset-password
     * @param {string} token
     * @param {string} newPassword
     * @returns {Promise<{success: boolean, code?: string, message: string}>}
     */
    async resetPassword(token, newPassword) {
        try {
            const response = await fetch("/auth/reset-password", {
                method: "POST",
                headers: { "Content-Type": "application/json", "Accept": "application/json" },
                credentials: "same-origin",
                body: JSON.stringify({ token, newPassword })
            });
            const data = await response.json();
            return {
                success: data.success ?? false,
                code: data.code ?? null,
                message: data.message ?? ""
            };
        } catch (err) {
            console.error("ResetPassword fetch hatas\u0131:", err);
            return { success: false, code: null, message: "Sunucuya ula\u015f\u0131lam\u0131yor." };
        }
    },

    /**
     * POST /auth/verify-2fa
     * @param {string} code - 6 haneli TOTP kodu
     * @returns {Promise<{success: boolean, code?: string, message: string}>}
     */
    async verify2FA(code) {
        try {
            const response = await fetch("/auth/verify-2fa", {
                method: "POST",
                headers: { "Content-Type": "application/json", "Accept": "application/json" },
                credentials: "same-origin",
                body: JSON.stringify({ code })
            });
            const data = await response.json();
            return {
                success: data.success ?? false,
                code: data.code ?? null,
                message: data.message ?? ""
            };
        } catch (err) {
            console.error("Verify2FA fetch hatas\u0131:", err);
            return { success: false, code: null, message: "Sunucuya ula\u015f\u0131lam\u0131yor." };
        }
    },

    /**
     * POST /auth/setup-2fa/initiate \u2014 secret \u00fcretir + QR URI d\u00f6ner
     */
    async initiate2FASetup() {
        try {
            const response = await fetch("/auth/setup-2fa/initiate", {
                method: "POST",
                headers: { "Accept": "application/json" },
                credentials: "same-origin"
            });
            const data = await response.json();
            return {
                success: data.success ?? false,
                secret: data.secret ?? null,
                otpAuthUri: data.otpAuthUri ?? null,
                message: data.message ?? ""
            };
        } catch (err) {
            console.error("Initiate2FASetup fetch hatas\u0131:", err);
            return { success: false, message: "Sunucuya ula\u015f\u0131lam\u0131yor." };
        }
    },

    /**
     * POST /auth/setup-2fa/confirm \u2014 kullan\u0131c\u0131 kodu girer, 2FA aktive olur
     */
    async confirm2FASetup(code) {
        try {
            const response = await fetch("/auth/setup-2fa/confirm", {
                method: "POST",
                headers: { "Content-Type": "application/json", "Accept": "application/json" },
                credentials: "same-origin",
                body: JSON.stringify({ code })
            });
            const data = await response.json();
            return {
                success: data.success ?? false,
                message: data.message ?? ""
            };
        } catch (err) {
            console.error("Confirm2FASetup fetch hatas\u0131:", err);
            return { success: false, message: "Sunucuya ula\u015f\u0131lam\u0131yor." };
        }
    },

    /**
     * POST /auth/setup-2fa/disable
     */
    async disable2FA(code) {
        try {
            const response = await fetch("/auth/setup-2fa/disable", {
                method: "POST",
                headers: { "Content-Type": "application/json", "Accept": "application/json" },
                credentials: "same-origin",
                body: JSON.stringify({ code })
            });
            const data = await response.json();
            return {
                success: data.success ?? false,
                message: data.message ?? ""
            };
        } catch (err) {
            console.error("Disable2FA fetch hatas\u0131:", err);
            return { success: false, message: "Sunucuya ula\u015f\u0131lam\u0131yor." };
        }
    },

    /**
     * GET /auth/me \u2014 aktif session info
     */
    async whoAmI() {
        try {
            const response = await fetch("/auth/me", {
                method: "GET",
                headers: { "Accept": "application/json" },
                credentials: "same-origin"
            });
            if (!response.ok) return null;
            return await response.json();
        } catch (err) {
            console.error("WhoAmI fetch hatas\u0131:", err);
            return null;
        }
    },

    /**
     * QR kod \u00e7izer. qrcode-generator CDN'den yan dinamik y\u00fckler.
     * @param {string} containerId - DOM element id
     * @param {string} otpAuthUri  - otpauth:// URI
     */
    async drawQrCode(containerId, otpAuthUri) {
        // qrcode-generator'\u0131 lazy y\u00fckle
        if (typeof window.qrcode === "undefined") {
            await new Promise((resolve, reject) => {
                const s = document.createElement("script");
                s.src = "https://cdn.jsdelivr.net/npm/qrcode-generator@1.4.4/qrcode.min.js";
                s.onload = resolve;
                s.onerror = reject;
                document.head.appendChild(s);
            });
        }

        const el = document.getElementById(containerId);
        if (!el) return;

        const qr = window.qrcode(0, 'M');  // auto type number, error correction M
        qr.addData(otpAuthUri);
        qr.make();
        el.innerHTML = qr.createImgTag(6, 8);  // cell=6px, margin=8
        // G\u00f6rseli konteynere uydur
        const img = el.querySelector("img");
        if (img) {
            img.style.width = "100%";
            img.style.height = "auto";
            img.style.display = "block";
        }
    }
};
