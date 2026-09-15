(() => {
    const editors = new Map();

    function release(key) {
        const state = editors.get(key);
        if (!state) return;
        state.resizeObserver.disconnect();
        state.canvas.removeEventListener("pointerdown", state.pointerDown);
        state.canvas.removeEventListener("pointermove", state.pointerMove);
        state.canvas.removeEventListener("pointerup", state.pointerUp);
        state.canvas.removeEventListener("pointercancel", state.pointerUp);
        URL.revokeObjectURL(state.url);
        editors.delete(key);
    }

    function canvasPosition(state, event) {
        const rectangle = state.canvas.getBoundingClientRect();
        return {
            x: (event.clientX - rectangle.left) * state.canvas.width / rectangle.width,
            y: (event.clientY - rectangle.top) * state.canvas.height / rectangle.height
        };
    }

    function resize(state) {
        const available = state.canvas.parentElement?.clientWidth ?? 0;
        if (available < 40 || !state.image.naturalWidth) return;
        const maximumWidth = Math.min(920, available);
        const maximumHeight = Math.min(620, window.innerHeight * .68);
        const scale = Math.min(
            maximumWidth / state.image.naturalWidth,
            maximumHeight / state.image.naturalHeight
        );
        const width = Math.max(1, Math.round(state.image.naturalWidth * scale));
        const height = Math.max(1, Math.round(state.image.naturalHeight * scale));
        state.canvas.width = Math.round(width);
        state.canvas.height = height;
        state.canvas.style.width = `${width}px`;
        draw(state);
    }

    function draw(state) {
        const context = state.canvas.getContext("2d");
        const scaleX = state.canvas.width / state.image.naturalWidth;
        const scaleY = state.canvas.height / state.image.naturalHeight;
        context.clearRect(0, 0, state.canvas.width, state.canvas.height);
        context.drawImage(state.image, 0, 0, state.canvas.width, state.canvas.height);
        context.fillStyle = "rgba(9, 12, 10, .58)";
        context.fillRect(0, 0, state.canvas.width, state.canvas.height);

        context.save();
        context.beginPath();
        state.points.forEach((point, index) => {
            const x = point.x * scaleX;
            const y = point.y * scaleY;
            if (index === 0) context.moveTo(x, y); else context.lineTo(x, y);
        });
        context.closePath();
        context.clip();
        context.drawImage(state.image, 0, 0, state.canvas.width, state.canvas.height);
        context.restore();

        context.beginPath();
        state.points.forEach((point, index) => {
            const x = point.x * scaleX;
            const y = point.y * scaleY;
            if (index === 0) context.moveTo(x, y); else context.lineTo(x, y);
        });
        context.closePath();
        context.lineWidth = 3;
        context.strokeStyle = "#f0bd58";
        context.stroke();

        state.points.forEach((point, index) => {
            const x = point.x * scaleX;
            const y = point.y * scaleY;
            context.beginPath();
            context.arc(x, y, 10, 0, Math.PI * 2);
            context.fillStyle = "#123d32";
            context.fill();
            context.lineWidth = 3;
            context.strokeStyle = "#fff3cf";
            context.stroke();
            context.fillStyle = "#ffffff";
            context.font = "700 11px sans-serif";
            context.textAlign = "center";
            context.textBaseline = "middle";
            context.fillText(String(index + 1), x, y);
        });
    }

    async function initialize(key, inputId, canvasId) {
        release(key);
        const input = document.getElementById(inputId);
        const canvas = document.getElementById(canvasId);
        const file = input?.files?.[0];
        if (!file || !canvas) throw new Error("A imagem selecionada não está mais disponível.");

        const url = URL.createObjectURL(file);
        const image = new Image();
        await new Promise((resolve, reject) => {
            image.onload = resolve;
            image.onerror = () => {
                URL.revokeObjectURL(url);
                reject(new Error("Não foi possível visualizar a imagem."));
            };
            image.src = url;
        });
        if (image.naturalWidth * image.naturalHeight > 40_000_000) {
            URL.revokeObjectURL(url);
            throw new Error("A resolução da imagem ultrapassa o limite permitido.");
        }

        const state = {
            key, canvas, image, url, dragging: null,
            points: [
                { x: image.naturalWidth * .15, y: image.naturalHeight * .20 },
                { x: image.naturalWidth * .85, y: image.naturalHeight * .20 },
                { x: image.naturalWidth * .85, y: image.naturalHeight * .80 },
                { x: image.naturalWidth * .15, y: image.naturalHeight * .80 }
            ]
        };
        state.pointerDown = event => {
            const position = canvasPosition(state, event);
            const scaleX = state.canvas.width / state.image.naturalWidth;
            const scaleY = state.canvas.height / state.image.naturalHeight;
            let nearest = -1;
            let distance = Number.POSITIVE_INFINITY;
            state.points.forEach((point, index) => {
                const dx = point.x * scaleX - position.x;
                const dy = point.y * scaleY - position.y;
                const candidate = dx * dx + dy * dy;
                if (candidate < distance) { distance = candidate; nearest = index; }
            });
            if (distance <= 34 * 34) {
                state.dragging = nearest;
                state.canvas.setPointerCapture(event.pointerId);
                event.preventDefault();
            }
        };
        state.pointerMove = event => {
            if (state.dragging === null) return;
            const position = canvasPosition(state, event);
            state.points[state.dragging] = {
                x: Math.max(0, Math.min(state.image.naturalWidth - 1, position.x * state.image.naturalWidth / state.canvas.width)),
                y: Math.max(0, Math.min(state.image.naturalHeight - 1, position.y * state.image.naturalHeight / state.canvas.height))
            };
            draw(state);
            event.preventDefault();
        };
        state.pointerUp = event => {
            state.dragging = null;
            if (state.canvas.hasPointerCapture(event.pointerId)) state.canvas.releasePointerCapture(event.pointerId);
        };
        state.resizeObserver = new ResizeObserver(() => resize(state));
        canvas.addEventListener("pointerdown", state.pointerDown);
        canvas.addEventListener("pointermove", state.pointerMove);
        canvas.addEventListener("pointerup", state.pointerUp);
        canvas.addEventListener("pointercancel", state.pointerUp);
        state.resizeObserver.observe(canvas.parentElement);
        editors.set(key, state);
        resize(state);
    }

    window.ageNexusCropper = {
        initialize,
        refresh(key) {
            const state = editors.get(key);
            if (!state) throw new Error("O ajuste desta imagem ainda não foi carregado.");
            resize(state);
        },
        getPoints(key) {
            const state = editors.get(key);
            if (!state) throw new Error("O ajuste desta imagem ainda não foi carregado.");
            return state.points.map(point => [point.x, point.y]);
        },
        dispose(key) { release(key); },
        disposeAll(prefix) {
            [...editors.keys()].filter(key => key.startsWith(prefix)).forEach(release);
        }
    };
})();
