// Only prevent default for specific keys
window.logProbsAutocomplete = {
    maybePreventDefault: function (e) {
        if (["Tab", "Enter", "ArrowUp", "ArrowDown", "Escape"].includes(e.key)) {
            e.preventDefault();
        }
    }
};
