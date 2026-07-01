(function () {
    const launcher = document.getElementById("assistantLauncher");
    const panel = document.getElementById("assistantPanel");
    const messagesEl = document.getElementById("assistantMessages");
    const suggestionsEl = document.getElementById("assistantSuggestions");
    const form = document.getElementById("assistantForm");
    const input = document.getElementById("assistantInput");

    if (!launcher || !panel || !form || !input) {
        return;
    }

    const WELCOME_TEXT =
        "Hello! I'm the PURC Tariff Assistant. Ask me how the reckoner works, or ask me about the calculation currently on screen.";

    const history = [];
    let loading = false;

    function getPageContextJson() {
        const el = document.getElementById("assistantPageContext");
        return el ? el.textContent : null;
    }

    function scrollToBottom() {
        messagesEl.scrollTop = messagesEl.scrollHeight;
    }

    function appendMessage(role, content) {
        const bubble = document.createElement("div");
        bubble.className = "assistant-message assistant-message-" + role;
        bubble.textContent = content;
        messagesEl.appendChild(bubble);
        scrollToBottom();
        return bubble;
    }

    function setLoading(value) {
        loading = value;
        if (value) {
            const bubble = appendMessage("assistant", "Thinking…");
            bubble.classList.add("assistant-message-loading");
            bubble.id = "assistantLoadingBubble";
        } else {
            const loadingBubble = document.getElementById("assistantLoadingBubble");
            if (loadingBubble) {
                loadingBubble.remove();
            }
        }
    }

    function setSuggestionsVisible(visible) {
        suggestionsEl.style.display = visible ? "flex" : "none";
    }

    async function send(question) {
        const trimmed = (question || "").trim();
        if (!trimmed || loading) {
            return;
        }

        setSuggestionsVisible(false);
        appendMessage("user", trimmed);
        history.push({ role: "user", content: trimmed });
        input.value = "";
        setLoading(true);

        try {
            const response = await fetch("/api/assistant/ask", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    question: trimmed,
                    history: history.slice(-10),
                    pageContextJson: getPageContextJson()
                })
            });

            const data = await response.json().catch(function () {
                return {};
            });

            setLoading(false);

            if (!response.ok) {
                appendMessage("error", data.message || data.error || "Something went wrong. Please try again.");
                return;
            }

            const answer = data.answer || "I could not generate an answer. Please rephrase your question.";
            appendMessage("assistant", answer);
            history.push({ role: "assistant", content: answer });
        } catch (error) {
            setLoading(false);
            appendMessage("error", "The AI service could not be reached. Please try again.");
        }
    }

    launcher.addEventListener("click", function () {
        panel.classList.toggle("is-hidden");
        launcher.classList.toggle("is-open");

        if (!panel.classList.contains("is-hidden") && messagesEl.childElementCount === 0) {
            appendMessage("assistant", WELCOME_TEXT);
        }
    });

    suggestionsEl.querySelectorAll("[data-suggestion]").forEach(function (button) {
        button.addEventListener("click", function () {
            send(button.getAttribute("data-suggestion"));
        });
    });

    form.addEventListener("submit", function (event) {
        event.preventDefault();
        send(input.value);
    });

    input.addEventListener("keydown", function (event) {
        if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            send(input.value);
        }
    });
})();
