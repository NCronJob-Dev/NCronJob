
function highlightElement(elementId) {
    const element = document.getElementById(elementId);
    let anim = 'color-shift-highlight';
    if (element) {
        element.classList.add(anim);
        setTimeout(() => {
            element.classList.remove(anim);
        }, 2000); // Remove class after 1 second
    }
}
