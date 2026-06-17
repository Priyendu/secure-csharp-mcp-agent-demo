from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from xml.sax.saxutils import escape


ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
DRAWIO = DOCS / "architecture-diagrams.drawio"
PDF = DOCS / "ARCHITECTURE.pdf"

INK = (24, 32, 35)
MUTED = (96, 112, 108)
LINE = (217, 213, 202)
TEAL = (11, 107, 111)
TEAL_DARK = (9, 79, 82)
GREEN = (37, 115, 78)
RED = (168, 50, 50)
AMBER = (154, 91, 0)
PANEL = (255, 255, 255)
BG = (244, 241, 234)


@dataclass(frozen=True)
class Box:
    id: str
    label: str
    x: int
    y: int
    w: int
    h: int
    fill: str
    stroke: str


def drawio_box(box: Box) -> str:
    style = (
        "rounded=1;whiteSpace=wrap;html=1;arcSize=8;"
        f"fillColor={box.fill};strokeColor={box.stroke};strokeWidth=2;"
        "fontColor=#182023;fontSize=13;spacing=10;"
    )
    return (
        f'<mxCell id="{box.id}" value="{escape(box.label)}" style="{style}" vertex="1" parent="1">'
        f'<mxGeometry x="{box.x}" y="{box.y}" width="{box.w}" height="{box.h}" as="geometry"/>'
        "</mxCell>"
    )


def drawio_edge(edge_id: str, source: str, target: str, label: str, dashed: bool = False) -> str:
    style = (
        "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;"
        "html=1;strokeColor=#0B6B6F;strokeWidth=2;endArrow=classic;"
    )
    if dashed:
        style += "dashed=1;"
    return (
        f'<mxCell id="{edge_id}" value="{escape(label)}" style="{style}" edge="1" parent="1" '
        f'source="{source}" target="{target}"><mxGeometry relative="1" as="geometry"/></mxCell>'
    )


def drawio_page(page_id: str, name: str, title: str, boxes: list[Box], edges: list[tuple[str, str, str, str, bool]]) -> str:
    cells = [
        '<mxCell id="0"/>',
        '<mxCell id="1" parent="0"/>',
        (
            f'<mxCell id="{page_id}_title" value="{escape(title)}" '
            'style="text;html=1;strokeColor=none;fillColor=none;fontSize=22;fontStyle=1;'
            'fontColor=#182023;align=center;verticalAlign=middle;" vertex="1" parent="1">'
            '<mxGeometry x="40" y="24" width="1120" height="40" as="geometry"/></mxCell>'
        ),
    ]
    cells.extend(drawio_box(box) for box in boxes)
    cells.extend(drawio_edge(*edge) for edge in edges)
    return (
        f'<diagram id="{page_id}" name="{escape(name)}">'
        '<mxGraphModel dx="1400" dy="900" grid="1" gridSize="10" guides="1" tooltips="1" '
        'connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1200" pageHeight="850" '
        'math="0" shadow="0"><root>'
        + "".join(cells)
        + "</root></mxGraphModel></diagram>"
    )


def write_drawio() -> None:
    overview_boxes = [
        Box("user", "Security / Release User\nBrowser or Desktop", 70, 190, 170, 100, "#EAF3F3", "#0B6B6F"),
        Box("ui", "SecureMcpAgentWeb\nRelease Review UI\n/api/review", 330, 120, 230, 140, "#FFFFFF", "#0B6B6F"),
        Box("desktop", "SecureMcpDesktopDemo\nNative WPF C# Client\n(starts server + rich UI)", 330, 300, 230, 130, "#E0F2FE", "#0369A1"),
        Box("shared", "SecureMcpShared\nShared Models\n(ReleaseIntent, ToolCallResult,\nReviewResult, TokenResponse)", 580, 200, 260, 110, "#F0FDF4", "#166534"),
        Box("llm", "OpenAI Responses API\nOptional intent parsing\nOptional synthesis", 880, 80, 200, 110, "#F7FBFA", "#25734E"),
        Box("fallback", "Deterministic fallback\nOffline parser + summary", 880, 220, 200, 100, "#FFF8EB", "#9A5B00"),
        Box("mcp", "SecureMcpServer\nJWT + scopes\n/mcp/messages", 1120, 150, 180, 140, "#FFFFFF", "#0B6B6F"),
        Box("tools", "Release tools\nstatus\ndependencies\nvulnerabilities\napproval", 1120, 340, 180, 130, "#F7FBFA", "#25734E"),
        Box("data", "demo-data.json\nAnalyzerService ready\nPaymentGateway blocked", 880, 400, 200, 100, "#FFF8EB", "#9A5B00"),
    ]
    overview_edges = [
        ("e1", "user", "ui", "release question", False),
        ("e1b", "user", "desktop", "release question", False),
        ("e2", "ui", "llm", "if OPENAI_API_KEY", True),
        ("e3", "ui", "fallback", "fallback mode", True),
        ("e3b", "desktop", "fallback", "always (deterministic)", True),
        ("e4", "ui", "shared", "uses shared models", False),
        ("e4b", "desktop", "shared", "uses shared models", False),
        ("e4c", "ui", "mcp", "JWT + JSON-RPC tool calls", False),
        ("e4d", "desktop", "mcp", "JWT + JSON-RPC tool calls\n(or launches server)", False),
        ("e5", "mcp", "tools", "dispatch", False),
        ("e6", "tools", "data", "read demo releases", False),
        ("e7", "mcp", "ui", "tool trace + auth result", False),
        ("e7b", "mcp", "desktop", "tool trace + auth result", False),
    ]
    sequence_boxes = [
        Box("browser", "Browser UI", 80, 120, 150, 70, "#EAF3F3", "#0B6B6F"),
        Box("agent", "Agent API\nSecureMcpAgentWeb", 320, 120, 180, 70, "#FFFFFF", "#0B6B6F"),
        Box("planner", "LLM Planner\nor fallback", 590, 120, 170, 70, "#F7FBFA", "#25734E"),
        Box("server", "MCP Server", 850, 120, 160, 70, "#FFFFFF", "#0B6B6F"),
        Box("release", "Release Tools", 1060, 120, 160, 70, "#FFF8EB", "#9A5B00"),
        Box("s1", "1. POST /api/review\nquery + approvalMode", 90, 270, 260, 70, "#FFFFFF", "#D9D5CA"),
        Box("s2", "2. Parse intent\ncomponent + version", 370, 270, 240, 70, "#FFFFFF", "#D9D5CA"),
        Box("s3", "3. POST /auth/token\nmcp:tools (+ release scope)", 650, 270, 250, 70, "#FFFFFF", "#D9D5CA"),
        Box("s4", "4. Call MCP tools\nstatus, deps, vulns", 900, 390, 250, 70, "#FFFFFF", "#D9D5CA"),
        Box("s5", "5. Conditional approve_release\nserver enforces scope", 650, 510, 260, 70, "#FFFFFF", "#D9D5CA"),
        Box("s6", "6. Verdict + recommendation\ntool trace + raw JSON", 250, 630, 300, 70, "#FFFFFF", "#D9D5CA"),
    ]
    sequence_edges = [
        ("se1", "browser", "agent", "/api/review", False),
        ("se2", "agent", "planner", "structured intent / summary", True),
        ("se3", "agent", "server", "token + tools/call", False),
        ("se4", "server", "release", "execute tools", False),
        ("se5", "release", "agent", "results", False),
        ("se6", "agent", "browser", "verdict", False),
        ("se7", "s1", "s2", "", False),
        ("se8", "s2", "s3", "", False),
        ("se9", "s3", "s4", "", False),
        ("se10", "s4", "s5", "", False),
        ("se11", "s5", "s6", "", False),
    ]
    boundary_boxes = [
        Box("standard", "Standard token\nscope: mcp:tools", 100, 190, 230, 110, "#F7FBFA", "#25734E"),
        Box("priv", "Privileged demo token\nscope: mcp:tools\nscope: mcp:tools:release", 100, 420, 230, 130, "#FFF8EB", "#9A5B00"),
        Box("readtools", "Read-only tools\nstatus, dependencies,\nvulnerabilities", 480, 190, 260, 120, "#FFFFFF", "#0B6B6F"),
        Box("approve", "approve_release\nreturns ticket only with\nrelease scope", 480, 420, 260, 130, "#FFFFFF", "#0B6B6F"),
        Box("audit", "Audit trail\nAuth success/failure\nTool calls\nTool errors", 900, 280, 220, 160, "#F7FBFA", "#25734E"),
        Box("blocked", "Blocked outcomes\n403 missing release scope\nblocked status\nopen CVE finding", 900, 500, 220, 150, "#FFF5F5", "#A83232"),
    ]
    boundary_edges = [
        ("be1", "standard", "readtools", "allowed", False),
        ("be2", "standard", "approve", "403", True),
        ("be3", "priv", "approve", "allowed", False),
        ("be4", "readtools", "audit", "logged", False),
        ("be5", "approve", "audit", "logged", False),
        ("be6", "approve", "blocked", "denied / blocked", True),
    ]
    pages = [
        drawio_page("overview", "01-System Overview", "Secure Release Review Agent - System Overview", overview_boxes, overview_edges),
        drawio_page("sequence", "02-Release Review Sequence", "Release Review Sequence - LLM Assisted, MCP Enforced", sequence_boxes, sequence_edges),
        drawio_page("boundaries", "03-Authorization Boundaries", "Authorization Boundaries and Control Points", boundary_boxes, boundary_edges),
    ]
    DRAWIO.write_text(
        '<mxfile host="app.diagrams.net" modified="2026-05-29T22:30:00.000Z" '
        'agent="Codex" etag="secure-mcp-agent-architecture" version="21.0.2">'
        + "".join(pages)
        + "</mxfile>\n",
        encoding="utf-8",
    )


class Pdf:
    def __init__(self) -> None:
        self.objects: list[bytes] = []
        self.pages: list[int] = []
        self.font_regular = self.add(b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")
        self.font_bold = self.add(b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>")
        self.page_parent = 0

    def add(self, body: bytes) -> int:
        self.objects.append(body)
        return len(self.objects)

    def page(self, commands: list[str]) -> None:
        stream = "\n".join(commands).encode("latin-1", "replace")
        content = self.add(b"<< /Length " + str(len(stream)).encode() + b" >>\nstream\n" + stream + b"\nendstream")
        page = self.add(
            b"<< /Type /Page /Parent 0 0 R /MediaBox [0 0 842 595] /Resources << /Font << /F1 "
            + str(self.font_regular).encode()
            + b" 0 R /F2 "
            + str(self.font_bold).encode()
            + b" 0 R >> >> /Contents "
            + str(content).encode()
            + b" 0 R >>"
        )
        self.pages.append(page)

    def save(self, path: Path) -> None:
        pages_obj = self.add(
            b"<< /Type /Pages /Kids ["
            + b" ".join(str(page).encode() + b" 0 R" for page in self.pages)
            + b"] /Count "
            + str(len(self.pages)).encode()
            + b" >>"
        )
        catalog = self.add(b"<< /Type /Catalog /Pages " + str(pages_obj).encode() + b" 0 R >>")
        for i, body in enumerate(self.objects):
            self.objects[i] = body.replace(b"/Parent 0 0 R", b"/Parent " + str(pages_obj).encode() + b" 0 R")
        pdf = bytearray(b"%PDF-1.4\n%\xe2\xe3\xcf\xd3\n")
        offsets = [0]
        for i, body in enumerate(self.objects, 1):
            offsets.append(len(pdf))
            pdf.extend(f"{i} 0 obj\n".encode())
            pdf.extend(body)
            pdf.extend(b"\nendobj\n")
        xref = len(pdf)
        pdf.extend(f"xref\n0 {len(self.objects)+1}\n".encode())
        pdf.extend(b"0000000000 65535 f \n")
        for offset in offsets[1:]:
            pdf.extend(f"{offset:010d} 00000 n \n".encode())
        pdf.extend(
            b"trailer\n<< /Size "
            + str(len(self.objects) + 1).encode()
            + b" /Root "
            + str(catalog).encode()
            + b" 0 R >>\nstartxref\n"
            + str(xref).encode()
            + b"\n%%EOF\n"
        )
        path.write_bytes(pdf)


def rgb(color: tuple[int, int, int]) -> str:
    return " ".join(f"{value / 255:.4f}" for value in color)


def txt(text: str) -> str:
    return text.replace("\\", "\\\\").replace("(", "\\(").replace(")", "\\)")


def wrap(text: str, width: int) -> list[str]:
    words = text.split()
    lines: list[str] = []
    current = ""
    for word in words:
        candidate = f"{current} {word}".strip()
        if len(candidate) > width and current:
            lines.append(current)
            current = word
        else:
            current = candidate
    if current:
        lines.append(current)
    return lines or [""]


def text(cmds: list[str], x: float, y: float, value: str, size: int = 11, bold: bool = False, color: tuple[int, int, int] = INK) -> None:
    font = "F2" if bold else "F1"
    cmds.append(f"BT {rgb(color)} rg /{font} {size} Tf {x:.1f} {y:.1f} Td ({txt(value)}) Tj ET")


def rect(cmds: list[str], x: float, y: float, w: float, h: float, fill: tuple[int, int, int] = PANEL, stroke: tuple[int, int, int] = LINE, line_width: float = 1.0) -> None:
    cmds.append(f"q {rgb(fill)} rg {rgb(stroke)} RG {line_width:.1f} w {x:.1f} {y:.1f} {w:.1f} {h:.1f} re B Q")


def line(cmds: list[str], x1: float, y1: float, x2: float, y2: float, color: tuple[int, int, int] = TEAL, dashed: bool = False) -> None:
    dash = "[6 4] 0 d" if dashed else "[] 0 d"
    cmds.append(f"q {rgb(color)} RG 1.5 w {dash} {x1:.1f} {y1:.1f} m {x2:.1f} {y2:.1f} l S Q")
    angle = 1 if x2 >= x1 else -1
    cmds.append(f"q {rgb(color)} rg {x2:.1f} {y2:.1f} m {x2 - 7 * angle:.1f} {y2 + 4:.1f} l {x2 - 7 * angle:.1f} {y2 - 4:.1f} l f Q")


def card(cmds: list[str], x: float, y: float, w: float, h: float, title: str, body: list[str], accent: tuple[int, int, int] = TEAL) -> None:
    rect(cmds, x, y, w, h, PANEL, LINE, 1)
    cmds.append(f"q {rgb(accent)} rg {x:.1f} {y + h - 8:.1f} {w:.1f} 8 re f Q")
    text(cmds, x + 14, y + h - 30, title, 12, True)
    yy = y + h - 50
    for item in body:
        for part in wrap(item, 34):
            text(cmds, x + 14, yy, part, 9, False, MUTED)
            yy -= 12


def header(cmds: list[str], subtitle: str) -> None:
    cmds.append(f"q {rgb(BG)} rg 0 0 842 595 re f Q")
    text(cmds, 42, 552, "Secure C# MCP Agent Demo", 18, True, INK)
    text(cmds, 42, 530, subtitle, 11, False, MUTED)
    cmds.append(f"q {rgb(TEAL)} rg 42 516 758 3 re f Q")


def write_pdf() -> None:
    pdf = Pdf()

    c: list[str] = []
    header(c, "Professional architecture overview - Web UI, Native WPF Desktop, LLM agent, secured MCP tool server")
    text(c, 42, 468, "Architecture Document", 34, True, INK)
    text(c, 42, 435, "Version 1.4 | June 2026 | Demo / educational", 13, False, MUTED)
    card(c, 42, 250, 230, 115, "Design Goals", ["Demonstrable release review UI", "LLM-assisted parsing and synthesis", "MCP server remains source of truth", "Authorization is visible and testable"], TEAL)
    card(c, 306, 250, 230, 115, "Security Boundary", ["The model cannot approve a release", "approve_release requires mcp:tools:release", "All tool calls are auditable"], GREEN)
    card(c, 570, 250, 230, 115, "Demo Modes", ["AnalyzerService: ready", "PaymentGateway: blocked by CVE", "Privileged mode: approval ticket"], AMBER)
    card(c, 42, 120, 230, 110, "Native Desktop Demo", ["WPF C# E2E client", "Launches MCP server", "Live traces + logs", "Manual tool explorer"], (3, 105, 161))
    card(c, 306, 120, 230, 110, "Shared Models", ["SecureMcpShared lib", "ReleaseIntent / ToolCallResult", "ReviewResult / TokenResponse", "Used by Web + Desktop"], (22, 101, 52))
    text(c, 42, 145, "The web agent may call OpenAI for structured intent parsing and final narrative, but every verdict is grounded in MCP tool results.", 12, False, INK)
    pdf.page(c)

    c = []
    header(c, "System overview")
    card(c, 42, 360, 150, 95, "Browser UI", ["Release question", "Verdict", "Tool trace", "Raw JSON"], TEAL)
    card(c, 42, 240, 150, 95, "Desktop WPF", ["Native C# client", "Starts server", "Live traces + JWT lab"], (3, 105, 161))
    card(c, 250, 360, 180, 95, "SecureMcpAgentWeb", ["POST /api/review", "Planner orchestration", "Deterministic verdict"], TEAL)
    card(c, 250, 240, 180, 95, "SecureMcpShared", ["Shared models lib", "Intent / ToolCallResult", "ReviewResult"], (22, 101, 52))
    card(c, 488, 440, 160, 80, "OpenAI Responses API", ["Optional structured outputs", "Intent + summary"], GREEN)
    card(c, 488, 300, 160, 80, "Fallback Planner", ["Offline parser", "Deterministic summary"], AMBER)
    card(c, 706, 360, 150, 95, "SecureMcpServer", ["JWT validation", "Scope checks", "MCP tools"], TEAL)
    card(c, 706, 205, 150, 95, "Release Data", ["demo-data.json", "Ready + blocked cases"], GREEN)
    line(c, 192, 408, 250, 408)
    line(c, 430, 415, 488, 480, dashed=True)
    line(c, 430, 390, 488, 340, dashed=True)
    line(c, 648, 480, 706, 420)
    line(c, 430, 408, 706, 408)
    line(c, 781, 360, 781, 300)
    text(c, 250, 235, "Key principle: the LLM assists with language; the MCP server enforces authorization and tool execution.", 13, True, INK)
    pdf.page(c)

    c = []
    header(c, "Sequence diagram - Release review")
    actors = [("Browser", 80), ("Agent API", 235), ("Planner/LLM", 395), ("MCP Server", 565), ("Release Tools", 720)]
    for name, x in actors:
        text(c, x, 482, name, 11, True)
        line(c, x + 35, 468, x + 35, 115, LINE)
    steps = [
        (130, 455, 300, "1. POST /api/review"),
        (285, 410, 460, "2. parse intent"),
        (285, 365, 630, "3. request JWT"),
        (285, 320, 630, "4. tools/call status, deps, vulns"),
        (630, 275, 785, "5. execute tool logic"),
        (285, 230, 630, "6. conditional approve_release"),
        (630, 185, 285, "7. tool trace + auth result"),
        (285, 140, 130, "8. verdict + recommendation"),
    ]
    for x1, y, x2, label in steps:
        line(c, x1, y, x2, y, TEAL, dashed="parse" in label)
        text(c, min(x1, x2) + 8, y + 9, label, 9, False, MUTED)
    text(c, 42, 70, "Approval is attempted only when release status is ready and no vulnerability findings are returned.", 11, False, INK)
    pdf.page(c)

    c = []
    header(c, "Authorization boundaries")
    card(c, 70, 360, 220, 105, "Standard Token", ["scope: mcp:tools", "Can read release state", "Cannot approve release"], GREEN)
    card(c, 70, 190, 220, 115, "Privileged Demo Token", ["scope: mcp:tools", "scope: mcp:tools:release", "Requires demo release secret"], AMBER)
    card(c, 385, 360, 230, 105, "Read Tools", ["get_release_status", "get_dependencies", "check_security_vulnerabilities"], TEAL)
    card(c, 385, 190, 230, 115, "Approval Tool", ["approve_release", "Returns ticket only with release scope", "403 otherwise"], TEAL)
    card(c, 675, 270, 150, 120, "Audit + Outcomes", ["Auth events", "Tool calls", "403 errors", "Blocked CVEs"], RED)
    line(c, 290, 415, 385, 415, GREEN)
    line(c, 290, 230, 385, 250, AMBER)
    line(c, 290, 390, 385, 250, RED, dashed=True)
    line(c, 615, 415, 675, 335, TEAL)
    line(c, 615, 250, 675, 335, TEAL)
    text(c, 42, 88, "This boundary is intentionally visible in the UI: standard mode produces Needs Approval Scope; privileged mode can produce Approved.", 11, False, INK)
    pdf.page(c)

    c = []
    header(c, "Runbook and hardening")
    card(c, 42, 360, 230, 110, "Run Locally", ["1. Start SecureMcpServer", "2. Start SecureMcpAgentWeb", "3. Open http://localhost:5080", "4. Try ready, blocked, approve"], TEAL)
    card(c, 306, 360, 230, 110, "Configuration", ["OPENAI_API_KEY optional", "OPENAI_MODEL default gpt-4o-mini", "MCP_SERVER_URL", "DEMO_RELEASE_CLIENT_SECRET"], GREEN)
    card(c, 570, 360, 230, 110, "Validation", ["dotnet restore", "dotnet build -c Release", "dotnet test -c Release", "Browser smoke checks"], AMBER)
    text(c, 42, 260, "Production hardening", 18, True, INK)
    hardening = [
        "Replace demo token endpoint with OAuth2/OIDC.",
        "Use asymmetric JWT signing and key rotation.",
        "Move secrets to managed secret storage.",
        "Send audit logs to an immutable sink.",
        "Add request limits, rate limiting, and full MCP transport tests.",
    ]
    y = 230
    for item in hardening:
        text(c, 58, y, f"- {item}", 12, False, INK)
        y -= 24
    pdf.page(c)
    pdf.save(PDF)


if __name__ == "__main__":
    DOCS.mkdir(exist_ok=True)
    write_drawio()
    write_pdf()
