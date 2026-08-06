(() => {
    const endpoint = path => new URL(path, document.baseURI);
    let reloadPending = false;
    const events = new EventSource(endpoint("events"));
    events.onmessage = () => {
        if (!reloadPending) {
            reloadPending = true;
            setTimeout(() => location.reload(), 150);
        }
    };

    document.addEventListener("click", async event => {
        const button = event.target.closest("button[data-action]");
        if (!button) return;

        button.disabled = true;
        const editor = button.closest("[data-editor]");
        const payload = {
            action: button.dataset.action,
            name: button.dataset.name || null,
            typeName: button.dataset.type || null,
            cronExpression: editor?.querySelector("[data-cron]")?.value || null,
            timeZoneId: editor?.querySelector("[data-timezone]")?.value || null,
            parameterJson: editor?.querySelector("[data-parameter]")?.value || null
        };

        try {
            const response = await fetch(endpoint("api/control"), {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });
            if (!response.ok) {
                const problem = await response.json();
                throw new Error(problem.error || "Dashboard control failed.");
            }
            location.reload();
        } catch (error) {
            button.disabled = false;
            window.alert(error.message);
        }
    });
})();
