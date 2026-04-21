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
                return { success: true, sessionId: data.sessionId };
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
    }
};
