// Keep this file as early-boot global helpers.
// Program.cs invokes bzsPreferences.getCultureCookie during WebAssembly startup.
(function () {
    var t = document.documentElement.getAttribute("data-theme");
    if (t === "system") {
        var mediaDark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)");
        document.documentElement.setAttribute("data-theme", mediaDark && mediaDark.matches ? "dark" : "light");
    }

    if (!window.bzsPreferences) {
        window.bzsPreferences = {};
    }

    window.bzsPreferences.getCultureCookie = function () {
        var name = ".AspNetCore.Culture=";
        var rows = document.cookie.split("; ");
        for (var i = 0; i < rows.length; i++) {
            if (rows[i].indexOf(name) === 0) {
                return decodeURIComponent(rows[i].substring(name.length));
            }
        }

        return null;
    };
})();
