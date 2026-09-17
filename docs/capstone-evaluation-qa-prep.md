# Capstone Evaluation — Question & Answer Preparation

**Project**: Loan Application & Compliance Review Assistant (CapGemini AI Launchpad, Use Case 4)
**Presenter**: Aayush Mathur
**Prepared**: 2026-09-17

---

## How to use this document

Sections A–H are questions you are likely to be asked, grouped by theme, each with a short
spoken answer and the code that backs it. Section I covers the hard questions — the ones
designed to find a weakness. Section J is a cheat sheet of numbers. Section K is the honest
list of gaps; read it before you present so nothing surprises you.

**A rule for the whole viva**: when a question touches a number or a rule, name the file.
"That's in `EligibilityCalculator.cs`" is worth more than a paragraph of description.

---

## A. Opening and framing

### A1. In one sentence, what does your project do?
It is a privacy-aware assistant that helps applicants understand loan products and submit
evidence, validates that evidence, runs deterministic eligibility calculations, and prepares
a recommendation for a human loan officer to approve — the assistant never makes the credit
decision itself.

### A2. Why is this a good use case for AI?
Because the work splits cleanly into two halves. The language-shaped half — explaining
products, reading messy documents, summarising evidence — is exactly what an LLM is good at.
The decision half — affordability arithmetic, threshold rules, approval — must be exact,
repeatable and auditable, which is exactly what an LLM is bad at. The architecture puts each
half where it belongs.

### A3. What is the single most important design decision you made?
That the eligibility outcome is computed by deterministic C# in the Domain layer and the
model is never allowed to override it. Everything else — agents, RAG, MCP — is built around
protecting that boundary.

### A4. Who are the users?
Four personas with server-side role enforcement: Applicant (ask, upload, confirm),
Loan Officer (correct facts, request checks, approve or return), Compliance Reviewer
(inspect audit evidence, review exceptions, but cannot alter source facts), and
Administrator (prompts, indexes, roles, telemetry — and no part in credit decisions).

---

## B. Architecture

### B1. Why Clean Architecture?
Because the business rules are the part that must not rot. Putting them in a Domain project
with zero dependencies means the DTI rule cannot accidentally acquire a dependency on Azure,
EF Core or ASP.NET. It also makes the rules unit-testable without any infrastructure — the
Domain suite runs in 25 ms.

### B2. Show me the dependency direction.
Four layers, dependencies pointing inward:
- **Domain** (`Loan.Domain`) — entities and rules. References nothing.
- **Application** (`Loan.Application`) — CQRS handlers, agents, and the ports
  (`IChatModel`, `IPolicyRetriever`, `IDocumentExtractor`, `IIdentityReader`,
  `IIncomeReader`, `ICreditReader`, repositories). Depends only on Domain.
- **Infrastructure** (`Loan.Infrastructure`) — implements those ports: Azure OpenAI,
  Azure AI Search, EF Core/SQL Server, MCP server, Blob storage, telemetry, resilience.
- **Web / Workers** — controllers, Razor views, SSE streaming, background indexing.

Web calls Application, never Infrastructure directly. Adapters are wired in the composition root.

### B3. How would you prove the Domain has no dependencies?
Open `Loan.Domain.csproj` — it has no `PackageReference` and no `ProjectReference` entries.
That is the proof, and it is enforced by the compiler, not by convention.

### B4. Why ports and adapters rather than calling Azure SDKs directly?
Three reasons: the Application layer stays testable with fakes (which is how the 59
Application tests run with no cloud calls); swapping a provider is an Infrastructure change
only; and it keeps vendor types out of the business logic.

### B5. What is in Workers and why separate?
Indexing policy documents and background document processing. They are separated because
they are long-running and must not block a web request, and they call Application through
the same ports the web does.

---

## C. The deterministic core — expect the hardest questions here

### C1. How exactly is DTI calculated?
In `src/Loan.Domain/Eligibility/EligibilityCalculator.cs`, as
`monthlyDebts / effectiveMonthlyIncome`, compared against `rules.MaxDtiRatio`. It is pure
C#, a static method, with no I/O and no model involvement.

### C2. What happens if income is zero?
There is an explicit guard. If `income <= 0`, the DTI ratio is set to `null` (not zero, and
not infinity), `isDtiEligible` is false, an unmet condition is recorded stating that DTI
cannot be performed, and the status becomes `Ineligible`. This is deliberate — returning 0
would read as a perfect DTI, which is the dangerous failure.

### C3. Is LTV always calculated?
No, and that is a product-awareness point. LTV is only applicable where
`rules.RequiresPropertyValuation` is true — a mortgage. For an unsecured personal loan, LTV
is `null` and `isLtvEligible` is set to true so it cannot wrongly block eligibility. For a
mortgage with a zero or missing property value, LTV is `null`, ineligible, and an unmet
condition is recorded.

### C4. What are the possible statuses and how are they chosen?
Four: `PendingInformation`, `Ineligible`, `ReferToHuman`, `Eligible`. They are evaluated in
a strict precedence order:
1. **PendingInformation** — a required identity or income verification is missing. This wins
   over everything, because you cannot judge an application you do not have the evidence for.
2. **Ineligible** — a hard failure: zero income, missing property value on a mortgage, credit
   score below minimum, income below product minimum, or loan amount above the ceiling.
3. **ReferToHuman** — ratio failures: DTI over threshold, or LTV over threshold on a mortgage.
4. **Eligible** — everything passed.

### C5. Why is a DTI failure "ReferToHuman" rather than "Ineligible"?
Because a ratio slightly over threshold is a judgement call a lender may still want to make
on compensating factors. A missing credit floor is a hard policy fail. Encoding that
difference is the point of having a status enum rather than a boolean.

### C6. Which value wins — what the applicant stated or what the tool returned?
The verified value, always. `ApplicantFacts` exposes `EffectiveMonthlyIncome` and
`EffectiveCreditScore`, which return the verified figure when the verification flag is set
and the value is present, and fall back to the stated figure otherwise. The calculator reads
only the `Effective*` properties, so it is structurally impossible for it to use a stated
value when a verified one exists. That is BR-02 enforced by design rather than by discipline.

### C7. How do you test boundaries?
With exact-threshold tests — an applicant exactly at the maximum DTI, exactly at the minimum
credit score — because off-by-one at a policy boundary is the classic lending defect. The
comparison is `<=` for maxima and `>=` for minima, tested at the boundary value itself.

---

## D. Multi-agent orchestration

### D1. Why three agents rather than one prompt?
Bounded mandates. Each agent has a narrow job and an explicit list of things it must not do,
which makes each one easier to constrain, evaluate and debug. A single prompt doing all three
jobs is one jailbreak away from doing all three badly.

### D2. What are the three agents and their limits?
- **Document Agent** (`DocumentAnalysisAgent.cs`) — summarises confirmed facts and identifies
  missing evidence. Explicitly forbidden from doing DTI/LTV arithmetic or underwriting decisions.
- **Eligibility Agent** (`EligibilityAnalysisAgent.cs`) — calls the verification tools and
  explains the factors from the *authoritative deterministic* calculation. Cannot override it.
- **Compliance Agent** (`ComplianceReviewAgent.cs`) — checks policy version, consent and
  citations, raises exceptions. Cannot alter facts or approve.

### D3. Walk me through the orchestration.
`RecommendationOrchestratorAgent.ProcessApplicationAsync` runs four instrumented stages —
`DocumentAnalysis`, `EligibilityAnalysis`, `ComplianceReview`, `OrchestratorSynthesis` — each
timed into telemetry. Then the critical step: the routing state is derived from
`application.Indicators.Status`, the deterministic result, via a `switch`:
`Eligible → ReadyForOfficerReview`, `PendingInformation → PendingInformation`,
`Ineligible → ManualReview`, `ReferToHuman → ManualReview`. The LLM's synthesis is used for
the narrative summary only. It never sets the state.

### D4. So what is the LLM actually for?
Explanation and drafting: turning the confirmed facts, tool results and deterministic outcome
into readable prose for the officer, and identifying what evidence is still missing. It is a
writer and a retriever, not an underwriter.

### D5. What temperature do you use and why?
0.1 for the orchestrator synthesis. The task is faithful summarisation of supplied facts, so
near-deterministic output is what you want — creativity here is a defect.

---

## E. RAG and grounding

### E1. How does retrieval work?
Hybrid search over Azure AI Search using `text-embedding-3-small` at 1536 dimensions,
combining vector and keyword matching, filtered to active product versions. Answers cite
source title, version and section.

### E2. Why hybrid rather than pure vector?
Because lending questions contain exact tokens — "80%", "TRID", "640" — that keyword search
nails and embeddings can blur. Hybrid gets the semantic recall of vectors and the precision
of literal matching.

### E3. What happens when the policy corpus has nothing relevant?
It returns an honest "we could not find sufficient matching policy evidence" with **zero**
citations. Two prompts in the evaluation set (`PROMPT-011`, `PROMPT-012` — foreign nationals,
crypto-collateral) exist specifically to prove the system refuses rather than invents.

### E4. What if Azure AI Search is unavailable?
It logs a warning, records a `SearchUnavailable` error to telemetry, and returns an empty
result set — deliberately *not* a synthetic fallback. A degraded search must produce a
"cannot verify" answer, never a confident ungrounded one.

### E5. How do you handle conflicting policy versions?
Retrieval is filtered to effective versions; where a conflict is detected it is flagged and
routed to a loan officer rather than silently resolved by the model.

---

## F. Security and safety

### F1. How do you handle PII?
`PiiMasker` (in `Loan.Application.Common`) masks before the model call: SSNs become
`***-**-6789`, account numbers keep only the last four digits, emails become `jo***@domain`.
Masked values are what reach the prompt, and telemetry records category and correlation ID
only — never sensitive content.

### F2. How do you defend against prompt injection?
`PromptInjectionGuard` short-circuits the request before it reaches the model, matching a
list of override patterns ("SYSTEM OVERRIDE", "IGNORE PREVIOUS INSTRUCTIONS",
"PRINT SYSTEM PROMPT", "APPROVE LOAN IMMEDIATELY", and similar) plus a regex for spacing
variants. It returns a fixed refusal that states the request was denied and logged.

### F3. (Hard) A keyword blocklist is weak — a paraphrase gets through. Why is that acceptable?
It is a first line, not the defence. Be direct about this: the guard reduces noise, but the
real protection is architectural. Even if a malicious instruction reaches the model, the model
cannot approve anything — `save_draft` rejects `Approve`/`Reject` arguments server-side, the
routing state comes from Domain code, and cross-application reads are blocked by server-side
scoping. The system is designed so that a successful prompt injection still cannot produce a
credit decision. That is defence in depth, and it is the answer they want to hear.

### F4. How is cross-application access prevented?
Server-side scope validation on every MCP tool call — `ValidateApplicationScopeAsync` checks
the `ApplicationId` and `SyntheticId` pair before any read. It is not a UI filter; it happens
on the server, and the response never confirms whether another application exists.

### F5. How do you stop the LLM approving a loan?
Three independent mechanisms: (1) the routing state is derived from the deterministic Domain
status, not from model output; (2) the `save_draft` MCP tool explicitly rejects `Approve`,
`Reject`, `Approved` and `Rejected` routing states with an `InvalidArguments` error; (3) only
an authenticated officer role can change recommendation status, and every change writes an
audit entry. Any one of these failing still leaves two.

---

## G. MCP and tooling

### G1. What is MCP and why use it?
Model Context Protocol — a typed, standard way to expose a bounded set of enterprise
operations to a model. Using it means the tool surface is explicit, allow-listed and
auditable rather than the model having open access to a database.

### G2. What tools do you expose?
Five: `get_identity_status`, `get_income_verification`, `get_credit`, `search_policy`,
`save_draft`. Served over JSON-RPC 2.0 on Streamable HTTP at `POST /api/mcp`.

### G3. Why is there an income tool when the spec lists only four?
FR-04 requires identity, income *and* credit retrieval, so an income lookup is needed to
satisfy it. The spec's four were representative, not exhaustive, and the added tool follows
the same rules — typed, authenticated, scoped, allow-listed.

### G4. What stops a tool being misused?
Every call validates arguments, checks server-side application scope, and is restricted to
approved read operations — except `save_draft`, which is a write and therefore carries the
explicit Approve/Reject rejection and zero retries.

---

## H. Quality, telemetry and operations

### H1. What does your test suite cover?
Six suites mirroring the production boundaries, **161 tests, all passing**:
Domain 20, Application 59, Contract 4, Integration 23, Prompt 21, End-to-End 34.

### H2. How do you evaluate the AI itself?
A repeatable 20-prompt suite — 15 golden, 5 adversarial — with a 100% pass rate, producing
`evaluation_results.json` and an auditable `evaluation_report.md`. Each prompt records
expected-keyword match, citation count, disclaimer presence, latency and tokens.
Categories: GroundedRAG (8), FinancialIntegrity (2), MissingEvidence (2),
OfficerExclusivity (2), PromptInjection (5), boundary case (1).

### H3. (Hard) Your evaluation shows 0 tokens and 0.02 ms latency. Why?
Be straightforward: that run used the offline deterministic harness (`SyntheticChatModel`),
so those figures measure the harness, not the model. The value of that run is behavioural —
it proves refusals, citations and disclaimers hold. Capturing real token and latency
baselines against `gpt-4o` is the next step, and it is on the roadmap slide. Owning this
before they push on it is much stronger than defending it.

### H4. What do you measure in production telemetry?
Prompt/completion/total tokens, LLM and RAG latencies, per-agent stage latencies, tool call
counters, and errors — exposed via `GET /health/telemetry`, with `X-Correlation-ID`
threaded through requests. Health endpoints: `/health`, `/health/ready`, `/health/details`.

### H5. What happens when Azure OpenAI is slow or down?
`ResiliencePolicy` provides bounded retries with exponential backoff and jitter, 10-second
call timeouts, caller cancellation guards, and per-dependency circuit breakers
(Closed/Open/HalfOpen), with a safe degraded fallback. Critically, consequential writes get
**zero** retries — retrying a write risks duplicate state changes.

### H6. How are secrets handled?
User Secrets locally, configuration separated from code, environment variables for
deployment, and nothing committed. Managed identity / safe credential chain is the target
for cloud.

---

## I. The hard questions — prepare these carefully

### I1. "Isn't this just a wrapper around GPT-4o?"
No — and the evidence is that the most important logic runs with no model at all. The
eligibility outcome, the status precedence and the approval gate are pure C# and are covered
by tests that never call a model. The LLM handles explanation and retrieval. If you removed
it, the system would still compute correct eligibility; it would just be less helpful to read.

### I2. "What is the weakest part of your system?"
Give a real answer, not a humble-brag. The strongest honest answer: *the prompt-injection
guard is a pattern blocklist, which paraphrase will eventually defeat.* I mitigated it
architecturally — injection cannot yield an approval — but the detection layer itself
deserves a classifier or an LLM-based judge rather than a keyword list. Second honest gap:
the evaluation suite has been run offline, so I do not yet have real latency and token
baselines.

### I3. "Why should a bank trust this more than a human?"
It should not replace the human — that is the whole design. The claim is narrower and more
defensible: it makes the human faster and more consistent by assembling complete, cited,
provenance-tracked evidence, and by computing ratios identically every time. The officer
still decides.

### I4. "What happens if a verification tool fails mid-assessment?"
The affected factor is marked unavailable rather than assumed. That typically leaves the
application in `PendingInformation`, which is the correct conservative outcome — an
unavailable check is not a passed check.

### I5. "Show me where a model output could still cause harm."
The honest answer is the narrative summary — a model could describe the evidence
misleadingly even though it cannot change the decision. Mitigations: temperature 0.1, the
facts are supplied rather than recalled, the non-approval disclaimer is attached, and the
officer sees the structured deterministic indicators alongside the prose, not just the prose.

### I6. "How would you scale this to real bureau data?"
The port boundary is exactly the seam. `ICreditReader` would get a real adapter in
Infrastructure; nothing in Domain or Application changes. What *would* need to change is
everything around it — real PII handling, consent, data residency, contractual rate limits
and audit retention. I would not pretend that is a swap-the-adapter exercise.

### I7. "Why did you choose zero-shot or few-shot prompts?"
Answer from the artefacts: both were developed and compared, and the selected version is
recorded with its reason, following Ask-Review-Improve and Explain-Generate-Refine. Prompts
are versioned application assets, currently carrying an inline `Version v1.0` marker in
`AgentPrompts.cs`.

### I8. "Where is chain-of-thought stored?"
Nowhere — deliberately. The contract requests concise evidence and decision factors, never
hidden reasoning. Storing chain-of-thought in a regulated system creates a discovery liability
and adds no auditable value over the structured factors.

---

## J. Numbers cheat sheet

| Fact | Value |
|---|---|
| Framework | .NET 8, ASP.NET Core MVC + Razor |
| Projects | 5 source (Domain, Application, Infrastructure, Web, Workers) + 6 test |
| Tests | **161 passing, 0 failing** (Domain 20 · App 59 · Contract 4 · Integration 23 · Prompt 21 · E2E 34) |
| Evaluation | 20 prompts (15 golden + 5 adversarial), 100% pass |
| Azure OpenAI | `Azure.AI.OpenAI` 2.1.0, `gpt-4o` |
| Semantic Kernel | 1.34.0 |
| Azure AI Search | 11.6.0, `text-embedding-3-small`, 1536 dimensions, hybrid |
| EF Core / SQL Server | 8.0.8, migration `InitialCreate` |
| Azure Blob Storage | 12.21.0, with local fallback |
| MCP | JSON-RPC 2.0, Streamable HTTP, `POST /api/mcp`, 5 tools |
| Low-confidence threshold | 0.85 |
| Synthesis temperature | 0.1 |
| Call timeout | 10 seconds |
| Retries on consequential writes | 0 |
| Business rules | BR-01 … BR-07 |
| Functional requirements | FR-01 … FR-10 |
| Personas | 4 (Applicant, Loan Officer, Compliance Reviewer, Administrator) |
| Document categories | 5 |
| Synthetic verification records | 12 |
| Policy corpus | 8 versioned documents |

---

## K. Known gaps — know these before they find them

Being first to name a gap reads as rigour. Being caught not knowing it reads as carelessness.

1. **`promptVersion` is not on the recommendation draft.** The schema in the spec lists it,
   and the prompts carry an inline `Version v1.0` string, but there is no `promptVersion`
   field populated on the output. If asked: acknowledge it, note the prompts are versioned
   assets, and say surfacing the identifier on the draft is a small, known fix.
2. **Evaluation ran offline.** Token and latency figures are from the deterministic harness,
   not `gpt-4o`. Behavioural assertions are valid; performance baselines are not yet captured.
3. **Slice 10 is in progress.** Telemetry, evaluation and resilience are built; cloud
   deployment and Application Insights / OpenTelemetry export are the remaining work.
4. **Prompt-injection detection is pattern-based.** Mitigated architecturally (see F3), but
   the detector itself is the weakest single component.
5. **Documentation inconsistency.** `implementation-status.md` cites both "147 tests" and
   "34 tests" in different sections. The verified figure is **161**. Worth correcting in the
   doc before submission.
6. **All data is synthetic.** By design and per the spec's out-of-scope list — no production
   bureau access, no real customer data.

---

## L. Closing statement

If given a final word, land the thesis rather than listing features:

> "The hard part of this problem was not getting an LLM to talk about loans — it was making
> sure it could never decide one. Grounded retrieval with citations, deterministic domain
> rules for every ratio and threshold, bounded agents that cannot exceed their mandate, and
> an officer approval gate with an audit trail. The assistant prepares the decision. A person
> makes it."
