document.addEventListener("DOMContentLoaded", () => {
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
