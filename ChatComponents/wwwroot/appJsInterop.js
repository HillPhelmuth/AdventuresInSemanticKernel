// This is a JavaScript module that is loaded on demand. It can export any number of
// functions, and may import other JavaScript modules if required.

export function scrollDown(element) {
    if (!element) return;
    element.scrollTop = element.scrollHeight;
}
export function addCodeStyle(element) {
    if (!element) {
        console.log("No element in addCodeStyle");
        return;
    }
    if (!Prism) {
        console.log("No Prism in addCodeStyle");
        return;
    }

    Prism.highlightAllUnder(element);
}