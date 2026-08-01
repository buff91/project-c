import { app } from "/scripts/app.js";
import { api } from "/scripts/api.js";

const ROOT = "/project-c/live";
const EVENT_TYPES = [
    "execution_start",
    "execution_cached",
    "executing",
    "progress",
    "executed",
    "execution_success",
    "execution_error",
    "execution_interrupted",
];

let activePromptId = null;
let loadingPromptId = null;

async function post(path, value) {
    return api.fetchApi(`${ROOT}/${path}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(value),
    });
}

async function heartbeat() {
    if (!api.clientId) {
        return;
    }
    try {
        await post("session", { client_id: api.clientId });
    } catch (error) {
        console.warn("[Project-C Live] session heartbeat failed", error);
    }
}

async function loadPendingRun() {
    if (!api.clientId || loadingPromptId) {
        return;
    }
    try {
        const response = await api.fetchApi(
            `${ROOT}/run/next?client_id=${encodeURIComponent(api.clientId)}`,
        );
        if (response.status === 404) {
            return;
        }
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }
        const run = await response.json();
        loadingPromptId = run.prompt_id;
        await app.loadGraphData(run.workflow);
        activePromptId = run.prompt_id;
        await post(`run/${run.prompt_id}/loaded`, {});
        console.info(
            `[Project-C Live] loaded workflow for ${run.prompt_id}`,
        );
    } catch (error) {
        console.warn("[Project-C Live] workflow load failed", error);
    } finally {
        loadingPromptId = null;
    }
}

async function forwardEvent(type, event) {
    const data = event?.detail ?? {};
    const promptId = data.prompt_id ?? activePromptId;
    if (!promptId) {
        return;
    }
    try {
        await post("event", {
            prompt_id: promptId,
            type,
            data,
        });
    } catch (error) {
        if (type !== "status") {
            console.warn(
                `[Project-C Live] failed to forward ${type}`,
                error,
            );
        }
    }
}

app.registerExtension({
    name: "project-c.live-observability",

    async setup() {
        await heartbeat();
        await loadPendingRun();
        window.setInterval(heartbeat, 5000);
        window.setInterval(loadPendingRun, 500);
        for (const type of EVENT_TYPES) {
            api.addEventListener(
                type,
                (event) => void forwardEvent(type, event),
            );
        }
    },
});
