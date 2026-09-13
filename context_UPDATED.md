# Loan Application & Compliance Review Assistant — Implementation Context

## 1. Source of truth

This context is derived from the provided CapGemini AI Launchpad capstone specification, **“Use Case 4 Loan Application and Compliance Review Assistant.”** The implementation must follow the mandatory requirements in that document. Where this context proposes a concrete implementation detail that the source document does not prescribe (for example, an exact database product or UI framework component), it is explicitly marked as an **implementation decision**, not as a source requirement.

The capstone is a privacy-aware assistant that explains loan products, validates application evidence, uses trusted verification services, calculates deterministic eligibility indicators, and prepares a recommendation for **human review**. The LLM must never become the credit decision maker.

---

## 2. Non-negotiable technology baseline

- Target framework: **.NET 8**
- Web architecture: **ASP.NET Core MVC** with Razor views for the web UI.
- Clean Architecture is mandatory.
- Azure OpenAI:
  - Chat deployment
  - Embedding deployment
- Azure AI Search for RAG.
- Semantic Kernel plugins/function calling or an equivalent orchestration approach.
- Bounded specialist agents:
  - Document Agent
  - Eligibility Agent
  - Compliance Agent
- One MCP server exposing only approved, typed, scoped enterprise tools.
- NUnit tests.
- Integration tests.
- Repeatable prompt-evaluation dataset.
- Structured logging.
- OpenTelemetry and/or Application Insights.
- Token and latency measurement.
- Managed identity or safe local credential chain.
- Role-based authorization.
- No committed secrets.
- Cloud deployment with configuration separated from code.
- Documented rollback/fallback.

---

## 3. Clean Architecture dependency rules

### Domain
Owns:
- Applications
- ApplicantFacts
- ProductRules
- EligibilityIndicators
- Recommendations
- Domain exceptions
- Deterministic business rules

Must NOT reference:
- Azure SDKs
- Semantic Kernel
- ASP.NET Core
- Databases
- Logging frameworks
- Infrastructure technologies

### Application
Owns:
- Commands
- Queries
- Handlers
- DTOs
- Validators
- Interfaces / ports for:
  - chat model
  - retrieval
  - document extraction
  - identity
  - income
  - credit
  - persistence
  - approval
  - telemetry
  - tool access as needed

Depends only on Domain.

### Infrastructure
Implements Application interfaces and contains adapters for:
- Azure OpenAI
- Azure AI Search
- document extraction
- MCP
- persistence
- external verification APIs
- telemetry
- identity/auth support

Infrastructure components must be wired in the composition root.

### Web
Owns:
- ASP.NET Core MVC controllers
- Razor views
- request/response mapping
- authentication/authorization integration
- streaming presentation
- safe UI errors

Web calls Application only. No business rules belong here.

### Workers
Owns:
- indexing
- long-running processing
- evaluation workflows

Workers call Application through defined ports.

### Tests
Mirror production boundaries:
- Domain tests
- Application tests
- Contract tests
- Integration tests
- Prompt tests
- End-to-end tests

---

## 4. Business scope

### In scope
- Product-policy search
- Document intake
- Field extraction
- Missing-document detection
- Synthetic identity lookup
- Synthetic income lookup
- Synthetic credit lookup
- Deterministic affordability calculation
- Recommendation draft
- Officer approval
- Evaluation
- Monitoring

### Explicitly out of scope
- Autonomous lending decision
- Production bureau access
- Real customer data
- Fund disbursement
- Account creation
- Unbounded database queries
- Model-generated interest rates

---

## 5. Personas and permissions

### Applicant
Needs:
- Understand products
- Provide documents

Permitted:
- Ask questions
- Upload synthetic evidence
- Confirm extracted facts

### Loan Officer
Needs:
- Review a complete and explainable application

Permitted:
- Correct facts
- Request checks
- Approve recommendation
- Return recommendation

### Compliance Reviewer
Needs:
- Verify policy and regulatory controls

Permitted:
- Review exceptions
- Review audit evidence

Must NOT:
- Alter source facts

### Administrator
Needs:
- Govern AI and integrations

Permitted:
- Manage prompts
- Manage indexes
- Manage roles
- Manage limits
- Access telemetry

---

## 6. Functional requirements

| ID | Requirement | Priority |
|---|---|---|
| FR-01 | Answer loan-product and documentation questions with versioned citations. | Must |
| FR-02 | Upload synthetic application documents and extract candidate fields with provenance. | Must |
| FR-03 | Require applicant or officer confirmation of low-confidence fields. | Must |
| FR-04 | Retrieve synthetic identity, income and credit data through typed tools. | Must |
| FR-05 | Calculate debt-to-income ratio and rule outcomes in Domain services. | Must |
| FR-06 | Coordinate Document, Eligibility and Compliance agents with bounded steps. | Must |
| FR-07 | Prepare a schema-valid recommendation with facts, evidence and unresolved items. | Must |
| FR-08 | Require authorized officer approval before application-state changes. | Must |
| FR-09 | Expose approved verification and policy operations through MCP. | Should |
| FR-10 | Track groundedness, refusal, latency, token and tool metrics. | Must |

---

## 7. Business rules — release-critical

### BR-01
The LLM cannot:
- approve
- reject
- price
- disburse

a loan.

### BR-02
Identity, income and credit values must come from:
- timestamped tool results, or
- confirmed fields.

### BR-03
Debt-to-income and eligibility indicators are calculated by deterministic Domain rules.

### BR-04
Every product explanation cites the effective policy version.

### BR-05
Sensitive identifiers must be masked before model calls and must never be written to prompt logs.

Examples of sensitive data requiring protection include:
- PAN
- bank account values
- identity values
- other sensitive identifiers in synthetic documents

### BR-06
A recommendation with missing mandatory evidence remains:
**Pending Information**

### BR-07
Only an authorized loan officer may change recommendation status, and every consequential change requires an audit entry.

---

## 8. Mandatory end-to-end user workflow

### Flow A — Product question / grounded RAG
1. Applicant asks a product question.
2. Retriever filters to active product versions and approved applicant-facing content.
3. Assistant compares permitted features using cited evidence.
4. UI displays:
   - answer
   - assumptions
   - source title
   - section/page metadata
   - non-approval disclaimer
5. Conflicting versions are flagged and routed to a loan officer.
6. Insufficient evidence produces an honest cannot-verify response.

### Flow B — Document intake
1. Applicant uploads approved synthetic documents.
2. Validate file type and size.
3. Extract candidate fields.
4. Store field-level provenance.
5. Validate dates, income ranges and required identifiers.
6. Low-confidence fields require applicant/officer confirmation.
7. Missing fields stay blank.
8. Unsupported or suspicious files are rejected safely.
9. The model must not invent missing data.

### Flow C — Eligibility recommendation
1. Document Agent:
   - summarizes confirmed facts
   - identifies missing evidence
2. Eligibility Agent:
   - calls identity/income/credit tools
   - requests Domain calculation
3. Compliance Agent:
   - checks policy version
   - checks consent
   - checks citations
   - checks exceptions
4. Application creates a recommendation draft.
5. Officer:
   - approves
   - edits where permitted
   - returns for more information
6. Tool failure marks the affected factor unavailable.
7. Out-of-policy conditions go to manual review; never become a model-made decision.

### Flow D — Prompt injection / data leakage
1. Adversarial document contains malicious instructions.
2. Retrieved content is clearly delimited and treated as untrusted evidence.
3. Cross-application access is blocked.
4. Unauthorized tool arguments are rejected by policy/validation.
5. Security telemetry records:
   - category
   - correlation ID
   - no sensitive content
6. Repeated attacks may terminate the session.
7. The response must not confirm whether another application exists.

---

## 9. Required application interfaces

These interfaces should be represented in Application and implemented in Infrastructure:

- `IChatModel`
  - structured completion
  - streaming completion

- `IPolicyRetriever`
  - filtered product/compliance retrieval
  - citations

- `IDocumentExtractor`
  - candidate field extraction
  - provenance

- `IIdentityReader`
  - synthetic verified identity status

- `ICreditReader`
  - synthetic credit score
  - timestamp

- `IIncomeReader`
  - synthetic income verification
  - timestamp

- `IRecommendationRepository`
  - persist draft
  - persist approval state

Additional ports should be introduced only when they support a real boundary and do not leak Infrastructure concepts into Application.

---

## 10. Data and integration model

### Policy index
Content:
- Product guides
- Eligibility rules
- Disclosures

Access:
- Version-filtered semantic retrieval

Metadata must support at minimum:
- source title
- version
- effective status/version
- section/page
- content type / audience where needed

### Document store
Content:
- synthetic payslips
- synthetic IDs
- synthetic bank statements

Access:
- type/size validation
- background extraction

### Verification APIs
Content:
- synthetic identity
- synthetic income
- synthetic credit

Access:
- read-only validated tools

### Eligibility service
Responsibilities:
- DTI calculation
- thresholds
- exception reasons

Important:
- deterministic code, not model arithmetic
- rule results are authoritative

### MCP tools
The specification names these representative tools:
- `get_identity_status`
- `get_credit`
- `search_policy`
- `save_draft`

The implementation may add an income lookup tool because FR-04 requires identity, income and credit retrieval, but tools must remain:
- typed
- authenticated
- scoped
- allow-listed
- limited to approved operations

---

## 11. Prompt engineering contract

Prompts are versioned application assets.

Reusable system prompt must define:
- role
- permitted scope
- grounding rule
- citation rule
- refusal behavior
- output schema

The minimum contract from the assignment is:

**ROLE**
Loan intake assistant, not a credit decision maker.

**CONTEXT**
`CONFIRMED_FACTS`, `POLICY_SOURCES`, and `TOOL_RESULTS` are authoritative.

**RULES**
- Cite product statements.
- Mask identifiers.
- Never approve.
- Never reject.
- Never price.
- Never disburse.

**TASK**
Explain evidence, identify missing information, and prepare a reviewable recommendation.

**OUTPUT**
Valid JSON matching `RecommendationDraft` with citations and `unresolvedItems`.

Required development evidence:
- zero-shot version
- few-shot version
- comparison
- reason for selected prompt
- use Ask-Review-Improve
- use Explain-Generate-Refine
- versioned prompt assets

Never request or store hidden chain-of-thought. Request concise evidence and decision factors instead.

All structured outputs, tool arguments and citations must be validated. Invalid/insufficient results must be rejected or safely downgraded.

---

## 12. Recommended output schema

This section is an implementation design derived from the prompt contract and functional requirements.

Create a strict `RecommendationDraft` JSON contract containing at least:

- `applicationId`
- `status`
- `summary`
- `confirmedFacts`
- `verificationResults`
- `eligibilityIndicators`
- `policyEvidence`
- `unresolvedItems`
- `exceptions`
- `disclaimers`
- `generatedAt`
- `promptVersion`
- `correlationId`

Each citation should contain:
- source title
- version
- effective date/status if available
- section/page
- citation text/snippet or evidence reference

Do not allow model output to directly set the final approval status.

---

## 13. Agent boundaries

### Document Agent
Can:
- assemble confirmed fact sets
- identify missing documents
- summarize evidence

Cannot:
- verify eligibility
- make a decision
- override confirmed facts

### Eligibility Agent
Can:
- invoke verification tools
- request deterministic Domain calculations
- collect decision factors

Cannot:
- override deterministic results
- directly approve/reject
- invent values

### Compliance Agent
Can:
- check citations
- check consent
- check privacy
- check exceptions
- detect policy conflicts

Cannot:
- approve
- change application status

### Officer handler
The only controlled write path for recommendation-state changes.

Requires:
- authorization
- validation
- human approval
- audit entry

---

## 14. Security, privacy, and resilience controls

### Identity
- managed identity in cloud, or safe local credential chain
- no secrets in source control

### Authorization
- role checks in handlers
- role checks at write adapters where appropriate
- denied-action tests

### Prompt injection
- treat retrieved text/documents as untrusted data
- delimit external evidence
- allow-list tool calls
- validate tool arguments independently of model intent
- isolate applications/tenants
- prevent cross-application access

### Privacy
- mask PAN/account/identity values before model calls
- do not place sensitive values into prompt logs
- minimize retention
- isolate application data

### Validation
Validate:
- incoming files
- DTOs
- extracted values
- structured model outputs
- citations
- tool arguments
- state transitions

### Resilience
External calls must support:
- cancellation
- timeout
- correlation ID
- retry with backoff where safe
- circuit breaker where appropriate
- safe fallback

Explicitly test:
- HTTP 429
- dependency outage
- unavailable verification factor

### Observability
Record, without sensitive content:
- correlation ID
- latency
- token usage
- retrieval hits
- tool calls
- agent steps
- failures
- groundedness
- refusal metrics

---

## 15. Data model — implementation baseline

These are concrete implementation entities suggested to keep the code aligned with the required use cases.

### Application
- Id
- ApplicantReference
- ProductCode
- State
- CreatedAt
- UpdatedAt
- Version/ConcurrencyToken

### ApplicantFacts
- FieldName
- Value / typed value
- Source
- Confidence
- Provenance
- ConfirmationState
- ConfirmedBy
- ConfirmedAt

### Document
- Id
- ApplicationId
- DocumentType
- StorageReference
- ContentType
- Size
- UploadStatus
- ExtractionStatus
- SecurityScanStatus
- CreatedAt

### VerificationResult
- ApplicationId
- VerificationType
- Status
- Value
- Source
- RetrievedAt
- CorrelationId

### EligibilityIndicators
- DtiRatio
- DtiStatus
- RuleResults
- ExceptionReasons
- CalculatedAt

### RecommendationDraft
- Id
- ApplicationId
- Status
- Facts
- Evidence
- UnresolvedItems
- Exceptions
- GeneratedAt
- PromptVersion
- ApprovedBy
- ApprovedAt
- ReturnedReason

### AuditEntry
- Id
- ApplicationId
- ActorId
- ActorRole
- Action
- PreviousState
- NewState
- Timestamp
- CorrelationId
- SafeMetadata

Do not persist raw prompt transcripts containing sensitive identifiers.

---

## 16. Solution structure

Use this solution shape:

```text
LoanAssistant.sln

src/
  Loan.Domain/
    Applications/
    Products/
    Eligibility/
    Recommendations/
    Common/

  Loan.Application/
    Intake/
    ProductAdvice/
    Verification/
    Recommendations/
    Abstractions/

  Loan.Infrastructure/
    AzureOpenAI/
    Search/
    Documents/
    MCP/
    Persistence/
    Telemetry/

  Loan.Web/
    Controllers/
    Views/
    ViewModels/
    wwwroot/
    Authentication/
    Streaming/
    Composition/

  Loan.Workers/
    Indexing/
    Evaluation/
    Processing/

tests/
  Loan.Domain.Tests/
  Loan.Application.Tests/
  Loan.ContractTests/
  Loan.IntegrationTests/
  Loan.PromptTests/
  Loan.EndToEndTests/
```

Project references must preserve inward dependency direction.

---

## 17. MVC web experience

Implementation decision to satisfy the requested MVC format: use only Razor Views, HTML, CSS, and vanilla JavaScript. React, Angular, Vue, Blazor and other SPA frameworks are explicitly excluded.

### Pages
- Dashboard
- Product Advice
- Application Intake
- Document Review
- Eligibility Review
- Recommendation Review
- Audit / Quality
- Admin / Configuration

### MVC controllers
Keep controllers thin. Example responsibilities:
- map HTTP request to Application command/query
- validate model-binding level concerns
- authorize access
- stream AI output
- return safe errors

Do not place DTI formulas, eligibility rules, product rules, agent policy or repository logic in controllers.

### Streaming
Implement assistant response streaming from the Application port through Web. Use an ASP.NET Core-compatible streaming mechanism such as `IAsyncEnumerable`, streamed HTTP response, or another simple .NET 8 mechanism. Keep the mechanism behind the application boundary so it can be replaced.

Use Razor + HTML/CSS/vanilla JavaScript only. Do NOT use React, Angular, Vue, Next.js, Blazor, or other SPA frameworks. Build a beautiful, modern, responsive MVC UI.

---

## 18. Persistence

The source document does not mandate a specific SQL product.

Implementation decision:
- use Entity Framework Core with a relational database adapter behind Application abstractions.
- SQL Server/Azure SQL is a natural cloud-aligned option, but the Domain/Application layers must not depend on it.
- create EF Core configurations only in Infrastructure.

Support:
- concurrency control
- audit entries
- recommendation state transitions
- application isolation

---

## 19. RAG ingestion and retrieval

Provide 6–10 synthetic, versioned product/compliance documents.

Each indexed chunk should carry enough metadata for filtering and citation.

Ingestion workflow:
1. validate source file
2. identify source title/version
3. split into chunks
4. generate embeddings
5. index vectors + text + metadata
6. store source/page/section metadata
7. mark only approved/effective content retrievable for the relevant audience

Retrieval workflow:
1. receive query
2. apply mandatory metadata filters
3. semantic/vector retrieval
4. optionally rerank
5. enforce source/version policy
6. return evidence with citation metadata
7. if evidence is insufficient, return explicit cannot-verify state

Do not allow retrieved text to alter the tool policy or application rules.

---

## 20. Synthetic data

Provide:
- 6–10 versioned synthetic product/compliance documents
- 12 synthetic applications
- synthetic payslips
- synthetic IDs
- synthetic bank statements
- stub identity/income/credit APIs supporting:
  - success
  - unavailable
  - mismatch
- 20 evaluation prompts:
  - answerable
  - insufficient
  - ambiguous
  - adversarial

Never use real personal data.

---

## 21. Testing minimums

### Domain
At least **8 business-rule/boundary tests**.

Include:
- DTI boundary cases
- thresholds
- missing required evidence
- eligibility rule outcomes
- invalid values
- deterministic calculations

### Application
At least **6 handler tests** with mocked ports.

Cover:
- success
- denial
- validation failure
- missing evidence
- tool unavailable
- unauthorized write

### Integration
Cover:
- chat
- embedding/search
- document extraction adapter
- verification tools
- MCP contract
- persistence

### Prompt evaluation
Minimum:
- 15 golden questions
- 5 adversarial prompts

Measure:
- groundedness
- relevance
- refusal behavior

### API
Cover:
- authentication
- authorization
- validation
- streaming
- cancellation
- safe error contract

### Performance
At least:
- 5 concurrent users
- 1 long-running request
- latency captured
- no unhandled failures

---

## 22. Definition of done

The implementation is not complete until all of the following are true:

- clean checkout builds
- automated tests pass
- no API key committed
- no connection string committed
- no personal data committed
- no prompt transcript committed
- grounded answers show source title and section/page metadata
- unsupported questions are declined
- successful write actions are real, not fabricated
- at least one read tool and one controlled write tool run through Application interfaces
- streaming works
- cancellation works
- loading state visible
- safe error state visible
- logs contain correlation, latency, token, retrieval, agent and tool metrics without sensitive content
- README documents:
  - architecture
  - setup
  - configuration
  - tests
  - deployment
  - troubleshooting
  - known limitations
- architecture diagram exists
- ADR exists
- reproducible ingestion instructions exist
- prompt assets and evaluation results exist
- critical failure paths have traces/screenshots
- five-minute presentation material exists

### Trainer release blockers
A passing solution cannot:
- commit a secret
- fabricate a tool result
- perform a consequential write without approval
- expose data across users
- fail to show the source of a grounded enterprise answer

---

## 23. Required demo sequence

The final implementation must support this exact walkthrough:

1. Ask a product question and open the effective policy citation.
2. Upload a synthetic payslip and correct one low-confidence extracted value.
3. Run identity and credit tools and show deterministic DTI calculation tests.
4. Coordinate Document, Eligibility and Compliance agents to create a recommendation draft.
5. Attempt:
   - cross-application data extraction
   - unauthorized status change
   and show both are denied.
6. Approve a corrected recommendation as a loan officer.
7. Inspect audit and quality metrics.

---

## 24. Three-day implementation sequence

### Day 1 — Foundation / vertical slice
- Create .NET 8 solution
- Create Clean Architecture projects
- Define Domain model and deterministic rules
- Define Application ports
- Create MVC shell
- Add authentication/authorization boundary
- Create first chat use case
- Connect Azure OpenAI
- Add cancellation/correlation
- Add Domain tests
- Add Application tests
- Confirm vertical slice builds

Checkpoint:
- compiling solution
- domain tests pass
- application tests pass
- one chat path works

### Day 2 — RAG / tools / MCP
- synthetic documents
- ingestion pipeline
- embeddings
- Azure AI Search
- metadata filtering
- grounded citations
- document upload + extraction
- provenance
- verification ports
- typed MCP tools
- safe tool validation
- application persistence

Checkpoint:
- retrieval and tools work through Application interfaces

### Day 3 — Agents / safety / operations / deployment
- bounded agents
- recommendation workflow
- officer approval
- streaming UI
- security tests
- prompt evaluation
- OpenTelemetry/Application Insights
- token + latency metrics
- resilience tests
- deployment
- demo documentation
- README
- architecture diagram
- ADR
- quality report

Checkpoint:
- deployed end-to-end solution with quality evidence

---

## 25. Implementation decisions Claude must not violate

1. Use **.NET 8**.
2. Use **ASP.NET Core MVC**.
3. Keep Domain technology-agnostic.
4. Keep Azure/AI/Search/MCP/EF Core code in Infrastructure.
5. Application owns interfaces.
6. Web remains thin.
7. Deterministic loan calculations stay in Domain code.
8. LLM output is never trusted directly.
9. Tool arguments are validated server-side.
10. Write operations are authorization-protected and require human approval.
11. Never log sensitive identifiers or prompt transcripts.
12. No cross-application data access.
13. Use synthetic data only.
14. Do not create autonomous approve/reject logic.
15. Do not fabricate unavailable tool results.
16. Preserve traceability from answer -> source/version/page.
17. Make failure behavior explicit.
18. Make every major capability testable without live Azure dependencies.

---

## 26. Claude execution strategy

Claude should implement in small vertical slices and validate after every slice.

For every change:
1. inspect current repository structure
2. identify affected layer
3. preserve dependency rules
4. implement smallest coherent change
5. add/modify tests
6. run relevant tests
7. run build
8. report result
9. update documentation

Never do a giant unreviewed rewrite.

Before introducing any NuGet package:
- explain why it is needed
- confirm layer placement
- prefer stable .NET 8/Azure-supported packages
- avoid unnecessary dependencies

Before writing application-state changes:
- trace the complete authorization path
- validate command
- enforce business rule
- verify approval
- create audit entry
- only then persist

For AI-generated data:
- schema validate
- business validate
- sanitize/normalize
- attach provenance
- reject invalid results

---

## 27. Definition of successful implementation planning

Claude's first planning response must contain:

1. requirement traceability matrix mapping every FR/BR/scenario/security/quality requirement to a project/file/test
2. dependency diagram
3. solution/project tree
4. domain model
5. Application ports
6. Infrastructure adapters
7. MVC page/controller map
8. data model
9. RAG ingestion/retrieval design
10. agent workflow
11. MCP tool contract
12. security model
13. prompt/versioning plan
14. test strategy
15. three-day backlog
16. risk register
17. acceptance checklist
18. explicit list of assumptions / implementation decisions

Only after the plan is accepted should Claude proceed to broad implementation.

---

## 28. Reference architecture in one sentence

**MVC Web -> Application use cases/interfaces -> Infrastructure adapters -> Azure OpenAI / Azure AI Search / MCP / persistence / telemetry, while Domain remains deterministic and isolated.**


## 32. Persistent implementation management

Because the project has a large scope, implementation must be iterative and recoverable. Do not depend on conversation memory alone.

Maintain these repository files continuously:

```text
docs/implementation-plan.md
docs/task-checklist.md
docs/decision-log.md
docs/change-log.md
docs/implementation-status.md
```

Every implementation task must have a stable ID, requirement mapping, acceptance criteria, tests, status and Git checkpoint.

Use the workflow:

`Plan → Implement → Test → Review → Document → Commit → Update checklist → Next task`

At every step, maintain explicit **DONE / REMAINING / BLOCKED** status.

At the start of every new session, read `context.md` and the implementation-management files, inspect the repository and Git state, run the baseline tests, and resume from the last known-good checkpoint.

Never hide failures. Record them, create a focused recovery task, preserve the last good checkpoint, fix the issue, test it, commit it, and then resume.

Mandatory work always takes precedence over stretch goals.

The implementation plan must be designed so that changes to an Azure adapter, model, agent framework, database, or configuration can be made without destabilizing Domain and Application layers.


## 33. Token-efficient, user-visible implementation

This project should be implemented through an AI coding environment with limited/variable model context. Therefore:

- `context.md` and the `docs/` implementation-management files are persistent memory.
- Do not repeatedly reproduce unchanged requirements or architecture in chat.
- Load only the context relevant to the current task.
- Work in small, Git-commit-sized increments.
- Keep status reports compact.
- Record discoveries and decisions in repository documentation.
- Ask the user before major architecture/dependency/security/scope changes.
- Do not ask unnecessary questions for routine implementation.
- At each milestone, report what works, what does not, test evidence, remaining work, and the exact next step.
- Never claim completion merely because code was generated.

The implementation must remain portable across coding assistants and available models; do not assume a specific subscription or model capability.


## 34. UI technology constraint

The Web experience must use **ASP.NET Core MVC + Razor + HTML/CSS/vanilla JavaScript only**.

React, Angular, Vue, Next.js, Blazor and other SPA frameworks are explicitly not allowed.

Visual quality is still mandatory: the application should have a polished, modern, responsive enterprise/banking-style interface with professional layouts, cards, forms, tables, status indicators, document review screens, citation panels, loading/error states, streaming UI, and audit views.

Use reusable Razor partials, shared layouts, semantic HTML, CSS custom properties and modular vanilla JavaScript. Keep frontend tooling simple and avoid an unnecessary Node.js/SPA build pipeline.


## 35. Database and environment configuration strategy

### Local development

Use:
- **Microsoft SQL Server** for the local development database
- **SQL Server Management Studio (SSMS)** for database administration and inspection

Keep EF Core, migrations, repositories and provider-specific details in `Loan.Infrastructure`.

When the implementation reaches database setup, ask the user for the relevant local environment details instead of guessing them, for example:
- SQL Server instance name
- database name
- authentication method
- credentials only when actually required

Never place real credentials in source control.

### Production

Use **Azure SQL** for production.

The architecture must allow the application to move from local SQL Server to Azure SQL through configuration/infrastructure changes without changing Domain or Application code.

### Production configuration

The company account does not have Azure Key Vault access. Therefore the project must use:

**Azure App Service Configuration / Application Settings**

for production configuration and secrets.

Do NOT require:
- Azure Key Vault
- Key Vault references
- Key Vault SDKs
- Key Vault deployment steps

unless the user explicitly changes this decision later.

For local development, prefer:
- ASP.NET Core User Secrets
- environment variables

`appsettings.json` should contain only non-secret defaults/templates.

All environment-specific settings must remain outside source control.

## 36. Current project status

The user has already completed the initial Visual Studio setup through the architecture/reference verification stage.

Completed:
- `LoanAssistant.sln`
- .NET 8 source projects: `Loan.Domain`, `Loan.Application`, `Loan.Infrastructure`, `Loan.Web` (ASP.NET Core MVC), `Loan.Workers`
- NUnit test projects: `Loan.Domain.Tests`, `Loan.Application.Tests`, `Loan.ContractTests`, `Loan.IntegrationTests`, `Loan.PromptTests`, `Loan.EndToEndTests`
- Projects added to the solution
- Initial project references following Clean Architecture
- MVC selected for `Loan.Web`
- Initial dependency/reference verification completed

Implementation should continue from the existing repository and should not recreate this scaffolding.

Next:
1. verify repository/build state
2. establish/update persistent implementation-management docs
3. record setup as completed
4. start Domain/Application foundation
5. proceed iteratively with small Git checkpoints

Suggested task state:
- `FOUNDATION-001` Repository/setup verification — DONE
- `FOUNDATION-002` Create .NET 8 solution/projects — DONE
- `FOUNDATION-003` Configure project references — DONE
- `FOUNDATION-004` Verify MVC web project — DONE
- `FOUNDATION-005` Create Domain foundation — NEXT
- `FOUNDATION-006` Add deterministic Domain rules — NEXT
- `FOUNDATION-007` Add Domain tests — NEXT
- `FOUNDATION-008` Create Application abstractions — NEXT

Repository inspection remains authoritative if any item above differs from the actual state.
