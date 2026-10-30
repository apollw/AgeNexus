window.ageNexusDraft = {
    copy: async function (text) {
        await navigator.clipboard.writeText(text);
    },
    download: function (draft, filename) {
        const canvas = document.createElement("canvas");
        canvas.width = 1600;
        canvas.height = 900;
        const context = canvas.getContext("2d");
        const gold = "#d9a534", cream = "#f5efe2", forest = "#142a23", ink = "#17201d";
        context.fillStyle = cream;
        context.fillRect(0, 0, canvas.width, canvas.height);
        context.fillStyle = forest;
        context.fillRect(0, 0, canvas.width, 170);
        context.fillStyle = gold;
        context.fillRect(0, 160, canvas.width, 10);
        context.fillStyle = gold;
        context.font = "700 24px Manrope, sans-serif";
        context.fillText("AGE NEXUS · DRAFT DE CIVILIZAÇÕES", 75, 55);
        context.fillStyle = "#fff";
        context.font = "700 46px Unbounded, sans-serif";
        context.fillText(draft.title, 75, 120);
        context.fillStyle = ink;
        context.font = "700 24px Manrope, sans-serif";
        context.fillText(`${draft.mapName || "Mapa a definir"} · ${draft.mode}`, 75, 220);

        const teams = draft.teams || [];
        const columns = Math.min(4, Math.max(2, teams.length));
        const rows = Math.ceil(teams.length / columns);
        const cardWidth = (1450 - (columns - 1) * 22) / columns;
        const cardHeight = Math.min(530 / rows, 350);
        teams.forEach((team, index) => {
            const column = index % columns, row = Math.floor(index / columns);
            const x = 75 + column * (cardWidth + 22), y = 265 + row * (cardHeight + 22);
            context.fillStyle = "#fffaf0";
            context.strokeStyle = "#d7ccb6";
            context.lineWidth = 2;
            context.fillRect(x, y, cardWidth, cardHeight);
            context.strokeRect(x, y, cardWidth, cardHeight);
            context.fillStyle = forest;
            context.fillRect(x, y, cardWidth, 58);
            context.fillStyle = "#fff";
            context.font = "700 22px Unbounded, sans-serif";
            context.fillText(team.name, x + 20, y + 38);
            (team.players || []).forEach((player, playerIndex) => {
                const py = y + 95 + playerIndex * 66;
                context.fillStyle = ink;
                context.font = "700 22px Manrope, sans-serif";
                context.fillText(player.displayName, x + 22, py);
                context.fillStyle = "#9b6d16";
                context.font = "700 18px Manrope, sans-serif";
                context.fillText(player.civilization || "A definir", x + 22, py + 27);
            });
        });
        context.fillStyle = "#796f5e";
        context.font = "600 18px Manrope, sans-serif";
        context.fillText("Gerado no AgeNexus", 75, 855);
        const link = document.createElement("a");
        link.download = filename;
        link.href = canvas.toDataURL("image/png");
        link.click();
    }
};
