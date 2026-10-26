// Horloge UTC de la console. La navigation améliorée de Blazor remplace le contenu de l'élément :
// on le retrouve à chaque tick et on le remet à l'heure dès la fin d'une navigation.
const tick = () => {
    const now = new Date().toISOString().slice(11, 19);
    for (const clock of document.querySelectorAll("[data-utc-clock]")) {
        clock.textContent = now;
    }
};

tick();
setInterval(tick, 1000);
window.Blazor?.addEventListener("enhancedload", tick);
