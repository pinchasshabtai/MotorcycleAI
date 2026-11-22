const conversation = document.querySelector("#conversation");
const form = document.querySelector("#search-form");
const queryInput = document.querySelector("#query");
const sendButton = document.querySelector("#send-button");

form.addEventListener("submit", (event) => {
    event.preventDefault();
    void submitSearch();
});

queryInput.addEventListener("input", () => {
    queryInput.style.height = "auto";
    queryInput.style.height = `${Math.min(queryInput.scrollHeight, 150)}px`;
});

queryInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter" && !event.shiftKey) {
        event.preventDefault();
        form.requestSubmit();
    }
});

conversation.addEventListener("click", (event) => {
    const suggestion = event.target.closest(".suggestion");
    if (!suggestion) {
        return;
    }

    queryInput.value = suggestion.textContent.trim();
    queryInput.focus();
    queryInput.dispatchEvent(new Event("input"));
});

async function submitSearch() {
    const query = queryInput.value.trim();
    if (!query || sendButton.disabled) {
        return;
    }

    appendMessage("user", query);
    queryInput.value = "";
    queryInput.style.height = "auto";
    setLoading(true);
    const loadingMessage = appendLoadingMessage();

    try {
        const response = await fetch("/api/motorcycles/search", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ query })
        });
        const payload = await readResponse(response);
        loadingMessage.remove();
        await renderSearchResult(payload);
    } catch (error) {
        loadingMessage.remove();
        const detail = error instanceof Error
            ? error.message
            : "אירעה שגיאה לא צפויה.";
        appendMessage("assistant", detail, "לא הצלחתי להשלים את החיפוש");
    } finally {
        setLoading(false);
        queryInput.focus();
    }
}

async function readResponse(response) {
    const payload = await response.json().catch(() => null);
    if (!response.ok) {
        const detail = payload?.detail || payload?.title;
        throw new Error(detail || `הבקשה נכשלה (HTTP ${response.status}).`);
    }

    if (!payload || !Array.isArray(payload.recommendations)) {
        throw new Error("התקבלה תשובה לא תקינה מהשרת.");
    }

    return payload;
}

async function renderSearchResult(result) {
    const message = createMessage("assistant");
    const content = createMessageContent(message);
    appendTextParagraph(content, result.summary || "החיפוש הושלם.");

    if (result.recommendations.length === 0) {
        appendTextParagraph(content, "נסה לשנות את התקציב או את הדרישות ולחפש שוב.");
        conversation.append(message);
        scrollToLatest();
        return;
    }

    const list = document.createElement("div");
    list.className = "recommendation-list";
    for (const recommendation of result.recommendations) {
        list.append(createRecommendationCard(recommendation));
    }
    content.append(list);
    conversation.append(message);
    scrollToLatest();
}

function createRecommendationCard(recommendation) {
    const motorcycle = recommendation.motorcycle;
    const card = document.createElement("article");
    card.className = "recommendation-card";

    const header = document.createElement("div");
    header.className = "recommendation-header";
    const identity = document.createElement("div");
    const rank = document.createElement("p");
    rank.className = "recommendation-rank";
    rank.textContent = recommendation.rankLabel;
    const title = document.createElement("h3");
    title.textContent = `${motorcycle.manufacturer} ${motorcycle.model}`;
    identity.append(rank, title);

    const specs = document.createElement("p");
    specs.className = "bike-specs";
    specs.textContent = `${motorcycle.year} · ${motorcycle.engineCC} סמ״ק`;
    identity.append(specs);

    const score = document.createElement("span");
    score.className = "match-score";
    score.textContent = `${formatScore(recommendation.matchScore)}% התאמה`;
    header.append(identity, score);
    card.append(header);

    if (motorcycle.attributesAreEstimated) {
        const estimateNotice = document.createElement("p");
        estimateNotice.className = "estimate-notice";
        estimateNotice.textContent = "מאפייני ההתאמה מבוססים על הערכה כללית ולא על נתון יצרן.";
        card.append(estimateNotice);
    }

    const price = document.createElement("p");
    price.className = "bike-price";
    price.textContent = motorcycle.priceIsEstimate
        ? `מחיר משוער להמחשה בלבד: ${formatPrice(motorcycle.price)}`
        : formatPrice(motorcycle.price);
    card.append(price);

    if (recommendation.reason) {
        const reason = document.createElement("p");
        reason.className = "recommendation-reason";
        reason.textContent = recommendation.reason;
        card.append(reason);
    }

    const prosCons = document.createElement("div");
    prosCons.className = "pros-cons";
    prosCons.append(
        createListSection("יתרונות", recommendation.advantages, "pros"),
        createListSection("חסרונות", recommendation.disadvantages, "cons")
    );
    card.append(prosCons);
    return card;
}

function createListSection(title, items, className) {
    const section = document.createElement("section");
    section.className = className;
    const heading = document.createElement("h4");
    heading.textContent = title;
    section.append(heading);

    const list = document.createElement("ul");
    if (Array.isArray(items) && items.length > 0) {
        for (const item of items) {
            const listItem = document.createElement("li");
            listItem.textContent = item;
            list.append(listItem);
        }
    } else {
        const listItem = document.createElement("li");
        listItem.textContent = "לא צוין";
        list.append(listItem);
    }

    section.append(list);
    return section;
}

function appendMessage(role, text, headingText) {
    const message = createMessage(role);
    const content = createMessageContent(message);
    if (headingText) {
        appendTextParagraph(content, headingText);
    }
    appendTextParagraph(content, text);
    conversation.append(message);
    scrollToLatest();
    return message;
}

function createMessage(role) {
    const message = document.createElement("article");
    message.className = `message ${role === "user" ? "user-message" : "assistant-message"}`;
    if (role === "assistant") {
        const avatar = document.createElement("div");
        avatar.className = "message-avatar";
        avatar.setAttribute("aria-hidden", "true");
        avatar.textContent = "AI";
        message.append(avatar);
    }
    return message;
}

function createMessageContent(message) {
    const content = document.createElement("div");
    content.className = "message-content";
    message.append(content);
    return content;
}

function appendTextParagraph(parent, text) {
    const paragraph = document.createElement("p");
    paragraph.textContent = text;
    parent.append(paragraph);
}

function appendLoadingMessage() {
    const message = createMessage("assistant");
    const content = createMessageContent(message);
    const indicator = document.createElement("div");
    indicator.className = "loading-indicator";
    indicator.setAttribute("role", "status");
    const spinner = document.createElement("span");
    spinner.className = "spinner";
    spinner.setAttribute("aria-hidden", "true");
    const text = document.createElement("span");
    text.textContent = "מנתח את הבקשה ומחפש אופנועים...";
    indicator.append(spinner, text);
    content.append(indicator);
    conversation.append(message);
    scrollToLatest();
    return message;
}

function setLoading(isLoading) {
    sendButton.disabled = isLoading;
    queryInput.disabled = isLoading;
    sendButton.setAttribute("aria-busy", String(isLoading));
}

function formatPrice(price) {
    if (typeof price !== "number" || !Number.isFinite(price)) {
        return "לא זמין";
    }
    return new Intl.NumberFormat("he-IL", {
        style: "currency",
        currency: "ILS",
        maximumFractionDigits: 0
    }).format(price);
}

function formatScore(score) {
    if (typeof score !== "number" || !Number.isFinite(score)) {
        return "—";
    }
    return new Intl.NumberFormat("he-IL", { maximumFractionDigits: 0 }).format(score);
}

function scrollToLatest() {
    conversation.scrollIntoView({ block: "end", behavior: "smooth" });
}
