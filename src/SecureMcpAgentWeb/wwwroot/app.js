const queryInput = document.querySelector("#query");
const runButton = document.querySelector("#runReview");
const verdictBand = document.querySelector("#verdictBand");
const verdict = document.querySelector("#verdict");
const summary = document.querySelector("#summary");
const recommendation = document.querySelector("#recommendation");
const component = document.querySelector("#component");
const version = document.querySelector("#version");
const intentSource = document.querySelector("#intentSource");
const confidence = document.querySelector("#confidence");
const toolTrace = document.querySelector("#toolTrace");
const rawResponse = document.querySelector("#rawResponse");
const llmMode = document.querySelector("#llmMode");
const mcpServer = document.querySelector("#mcpServer");

document.querySelectorAll("[data-query]").forEach((button) => {
  button.addEventListener("click", () => {
    queryInput.value = button.dataset.query;
  });
});

runButton.addEventListener("click", runReview);

loadConfig();

async function loadConfig() {
  try {
    const response = await fetch("/api/config");
    const config = await response.json();
    llmMode.textContent = config.llmMode;
    mcpServer.textContent = config.mcpServerUrl;
  } catch {
    llmMode.textContent = "Config unavailable";
    mcpServer.textContent = "MCP unavailable";
  }
}

async function runReview() {
  const approvalMode = document.querySelector("[name='approvalMode']:checked").value;
  setBusy(true);
  try {
    const response = await fetch("/api/review", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        query: queryInput.value,
        approvalMode
      })
    });
    const payload = await response.json();
    if (!response.ok) {
      throw new Error(payload.error || "Review failed");
    }
    renderResult(payload);
  } catch (error) {
    renderError(error);
  } finally {
    setBusy(false);
  }
}

function renderResult(result) {
  verdictBand.className = `verdict-band ${result.verdict}`;
  verdict.textContent = formatVerdict(result.verdict);
  summary.textContent = result.summary;
  recommendation.textContent = result.recommendation;
  component.textContent = result.intent.component;
  version.textContent = result.intent.version;
  intentSource.textContent = result.intent.source;
  confidence.textContent = Number(result.intent.confidence).toFixed(2);
  rawResponse.textContent = JSON.stringify(result, null, 2);
  toolTrace.replaceChildren(...result.toolCalls.map(renderTool));
}

function renderTool(tool) {
  const item = document.createElement("article");
  item.className = "tool-item";

  const name = document.createElement("div");
  name.className = "tool-name";
  name.textContent = tool.tool;

  const status = document.createElement("span");
  status.className = `tool-status ${tool.status}`;
  status.textContent = `${tool.status.toUpperCase()} ${tool.httpStatus}`;

  item.append(name, status);
  return item;
}

function renderError(error) {
  verdictBand.className = "verdict-band blocked";
  verdict.textContent = "Error";
  summary.textContent = error.message;
  recommendation.textContent = "Start the MCP server and verify the agent configuration.";
  rawResponse.textContent = JSON.stringify({ error: error.message }, null, 2);
  toolTrace.replaceChildren();
}

function formatVerdict(value) {
  return value
    .split("_")
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

function setBusy(isBusy) {
  runButton.disabled = isBusy;
  runButton.textContent = isBusy ? "Running" : "Run Review";
}
