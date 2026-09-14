# Decision Log

Record of key architectural, technical, and implementation decisions for the project.

---

## ADR-001: Strict Clean Architecture Layer Boundaries
- **Date**: 2026-09-14
- **Status**: Accepted
- **Context**: The project spec requires Clean Architecture with strict layer dependency rules (`Loan.Domain` -> `Loan.Application` -> `Loan.Infrastructure` / `Loan.Web` / `Loan.Workers`).
- **Decision**: `Loan.Domain` will have ZERO third-party NuGet package dependencies (no EF Core, no Azure SDKs, no Semantic Kernel, no ASP.NET Core). All domain rules (DTI, LTV, Credit Score evaluation) are 100% deterministic C# logic (**BR-01**, **BR-03**).
- **Consequences**: Ensures high testability with standard NUnit unit tests and prevents LLM hallucinations from influencing financial rules.

---

## ADR-002: Modern Vanilla CSS + Vanilla JS Presentation Layer
- **Date**: 2026-09-14
- **Status**: Accepted
- **Context**: Requirement specifies ASP.NET Core MVC + HTML/CSS + vanilla JavaScript only (no heavy SPA frameworks like React/Angular/Vue/Blazor). UI must look presentation/demo-ready and polished.
- **Decision**: Custom responsive design system using CSS custom properties (variables), modern typography (Inter/Roboto), card grids, status badges, confidence meters, side-by-side document review, toast notifications, and modal dialogs built with vanilla JS.
- **Consequences**: Fast page loads, zero extra npm dependency bloat, native Razor view rendering with streaming capabilities where needed.

---

## ADR-003: EF Core Entity Mappers & JSON Column Serialization for Domain Aggregates
- **Date**: 2026-09-14
- **Status**: Accepted
- **Context**: Domain entities (`LoanApplication`, `Recommendation`, `ApplicantFacts`, `ProductRules`) contain rich value objects and collections. Domain classes must remain pure C# without EF Core annotations or parameterless constructors.
- **Decision**: Implemented `LoanApplicationEntity` and `RecommendationEntity` in `Loan.Infrastructure` using JSON serialization for value objects (`ApplicantFacts`, `ProductRules`, `EligibilityIndicators`, `Citations`, `AuditTrail`). Created `SqlLoanApplicationRepository` and `SqlRecommendationRepository` implementing `ILoanApplicationRepository` and `IRecommendationRepository`.
- **Consequences**: Full relational SQL Server database persistence without polluting the domain layer.

---

## ADR-004: Azure OpenAI SDK v2.1.0 Integration for IChatModel
- **Date**: 2026-09-14
- **Status**: Accepted
- **Context**: Slice 2 requires replacing the keyword-matching stub with a real Azure OpenAI Chat Completion model adapter connected to Azure AI Foundry (`gpt-4o` / `gpt-4o-mini`).
- **Decision**: Installed `Azure.AI.OpenAI` v2.1.0 (official Microsoft Azure SDK stable GA release). Created `AzureOpenAIChatModel` implementing `IChatModel` in `Loan.Infrastructure/AzureOpenAI/`. Configured endpoint, API key, deployment name, and embedding deployment name via ASP.NET Core User Secrets (`AzureOpenAI:Endpoint`, `AzureOpenAI:ApiKey`, `AzureOpenAI:DeploymentName`).
- **Consequences**: Live AI chat completions and streaming via real Azure OpenAI API with explicit startup validation, supporting structured JSON completion and cancellation tokens.

---

## ADR-005: Azure AI Search Hybrid Vector RAG & Active Policy Version Filtering
- **Date**: 2026-09-14
- **Status**: Accepted
- **Context**: Policy retrieval requires hybrid keyword + 1536-dim vector search (`text-embedding-3-small`), strict active/effective version filtering, and structured citations.
- **Decision**: Installed `Azure.Search.Documents` v11.6.0. Created `PolicyIndexDocument` with HNSW Cosine vector search profile. Built `PolicyIndexer` with idempotent `MergeOrUploadDocumentsAsync` batching and BOM-safe YAML frontmatter parsing (`EffectiveFrom`/`EffectiveTo`). Built `AzureAiSearchPolicyRetriever` implementing `IPolicyRetriever` with hybrid search, metadata filtering (`search.in(productId, ...)`), active version filtering (`isActive eq true`), hybrid RRF score thresholding (`score >= 0.0165`), and structured `CitationDto` generation.
- **Consequences**: Active policies (v2.0) are selected over expired policies (v1.0), 0 duplicate chunks are created on repeated application starts, and irrelevant queries return 0 results to prevent hallucinations.

---

## ADR-006: Explicit Runtime Exceptions for Missing Cloud Configurations (Zero Silent Fallbacks)
- **Date**: 2026-09-14
- **Status**: Accepted
- **Context**: Release-gate review requires that production runtime must NOT silently fall back to synthetic/in-memory test data when Azure AI Search or Azure OpenAI configuration is missing.
- **Decision**: Registered `AzureAiSearchPolicyRetriever` exclusively as `IPolicyRetriever` in production DI. In `AzureAiSearchPolicyRetriever`, missing credentials throw an explicit `InvalidOperationException` with clear guidance on setting User Secrets. `SyntheticPolicyRetriever` is reserved strictly for offline unit/contract/prompt/end-to-end tests.
- **Consequences**: Prevents deceptive test mock data from serving end users in misconfigured production/staging environments.

---

## ADR-007: Document Storage Abstraction & Local Disk Stream Persistence (`IDocumentStorageService`)
- **Date**: 2026-09-14
- **Status**: Accepted
- **Context**: Uploaded documents must NOT store raw file byte blobs inside `LoanApplication` domain aggregates or SQL Server JSON database columns.
- **Decision**: Created `IDocumentStorageService` abstraction in `Loan.Application.Abstractions` and implemented `LocalFileDocumentStorageService` in `Loan.Infrastructure.Documents`. Files are streamed directly to `App_Data/Uploads/{ApplicationId}/` using SHA-256 hash generation and sanitized GUID storage references (`DOC-STORE-{Guid.NewGuid():N}.bin`). Domain aggregates store only metadata, SHA-256 hash, and storage references.
- **Consequences**: Zero database bloat, low memory footprint via stream processing, and seamless replaceability for cloud storage (e.g., Azure Blob Storage) in future infrastructure iterations.

---

## ADR-011: Bounded Multi-Agent Specialist Framework, Deterministic Routing Overrides, and Tool Allow-Lists
- **Date**: 2026-09-14
- **Status**: Accepted
- **Context**: Slice 7 requires a multi-agent framework with specialist agents (`DocumentAnalysisAgent`, `EligibilityAnalysisAgent`, `ComplianceReviewAgent`, `RecommendationOrchestratorAgent`), tool permission allow-lists, deterministic `RoutingState` and `RiskScore` overrides, and strict non-approval safety guarantees.
- **Decision**:
  - Implemented 4 bounded specialist agents in `Loan.Application.Agents`.
  - Implemented explicit tool permission allow-lists: Document agent reads candidate fields; Eligibility agent reads immutable domain indicators; Compliance agent executes `IPolicyRetriever` RAG searches; Orchestrator agent calls `SaveRecommendationDraftCommandHandler`.
  - **Deterministic Routing & Risk Score Enforcement**: `RoutingState` and `RiskScore` are owned and assigned by the system derived directly from `EligibilityIndicators.Status` (`PendingInformation` -> `0.50`, `ReferToHuman` / `Ineligible` -> `0.65`/`0.85`, `Eligible` -> `0.15`). Any LLM mismatch is automatically overridden before persistence.
  - **Non-Approval Safety Boundary**: Orchestrator agent CANNOT issue `Approve` or `Reject` decisions, lock interest rates, or disburse funds. Recommendation drafts are saved exclusively in `DraftPreparedBySystem` status (**BR-07**).
- **Consequences**: Enables multi-agent collaboration with structured LLM reasoning summaries while guaranteeing zero numerical financial drift, strict tool authorization, and loan officer exclusivity for final status changes.

---

## ADR-012: Loan Officer Decision Exclusivity, Immutable Audit Trail, and SSE Streaming
- **Date**: 2026-09-14
- **Status**: Accepted
- **Context**: Slice 8 requires human-in-the-loop loan officer review interface, Server-Sent Events (SSE) streaming for multi-agent draft progress, strict Loan Officer exclusivity (**BR-07**), and immutable audit trail logging.
- **Decision**:
  - Implemented `StreamRecommendationDraft` endpoint in `OfficerController.cs` returning `text/event-stream` for live agent token streaming.
  - Enforced **BR-07 Exclusivity**: System agents produce status `DraftPreparedBySystem` only. Final status transitions (`Approved`, `Rejected`, `InformationRequested`) require authorized Loan Officer submission via `OfficerDecisionCommandHandler` with mandatory non-empty decision notes.
  - Implemented append-only immutable audit trail recording (`RecommendationAuditEntry` and `FieldOverrideAuditEntry`) rendered in `Review.cshtml` timeline.
- **Consequences**: Ensures 100% compliance with human-in-the-loop regulatory standards and full inspectability of application state changes.

---

## ADR-013: Security, Privacy, PII Masking, and Prompt Refusal Engine
- **Date**: 2026-09-14
- **Status**: Accepted
- **Context**: Slice 9 requires sensitive PII redaction, prompt injection defense, short-circuit refusal responses, and cross-application tenant isolation.
- **Decision**:
  - Implemented `PiiMasker` in `Loan.Application.Common` redacting SSNs (`***-**-1234`), account numbers (`******1234`), and emails.
  - Implemented `PromptInjectionGuard` in `Loan.Application.Common` checking keywords (`"SYSTEM OVERRIDE"`, `"IGNORE PREVIOUS INSTRUCTIONS"`, `"PRINT SYSTEM PROMPT"`) and regex patterns.
  - Short-circuited `AskProductQuestionQueryHandler` with standardized refusal response when injection attempt is detected, skipping RAG retrieval and LLM processing.
  - Enforced server-side application isolation across MCP server tools and document override commands.
- **Consequences**: Ensures PII protection, zero system prompt leakage, and automated refusal of malicious prompt injection attacks.


