// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Improve scroll performance by using passive event listeners
(function() {
    // Check if passive is supported
    let passiveSupported = false;
    try {
        const options = {
            get passive() {
                passiveSupported = true;
                return false;
            }
        };
        window.addEventListener("test", null, options);
        window.removeEventListener("test", null, options);
    } catch(err) {
        passiveSupported = false;
    }

    // Add passive touch event listeners to prevent scroll blocking
    if (passiveSupported) {
        document.addEventListener('touchstart', function() {}, { passive: true });
        document.addEventListener('touchmove', function() {}, { passive: true });
        document.addEventListener('wheel', function() {}, { passive: true });
    }
})();
