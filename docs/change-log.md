# Change Log

All notable changes to the Loan Application & Compliance Review Assistant project will be documented in this file.

## [Step 10.3 Resilience & Transient Policy Controls Passed] - 2026-09-15

### Added & Verified
- **Resilience Policy & Transient Controls (`Loan.Infrastructure.Resilience`, `Loan.EndToEndTests`)**:
  - Implemented `ResiliencePolicy` supporting configurable max retries (3), exponential backoff with random jitter, explicit caller-cancellation guards, standardized 10-second call timeouts, and circuit breaker state management (`Closed`, `Open`, `HalfOpen`).
  - **Explicit Cancellation Distinction**: Guarded against retrying normal caller cancellation (`cancellationToken.IsCancellationRequested == true`); caller cancellations immediately rethrow without retrying or opening circuit breaker. Internal call timeouts (10s) trigger bounded retries.
  - **Circuit Breaker Isolation**: Configured independent `ResiliencePolicy` instances per client so that an OpenAI circuit opening does not impact Azure AI Search operations.
  - Wrapped safe read-only Azure OpenAI completion calls in `AzureOpenAIChatModel` with `ResiliencePolicy`, returning a safe degraded fallback message (*"Degraded Service: AI model completion is temporarily unavailable. Detailed policy analysis must be conducted manually by an authorized loan officer."*) on outage or open circuit.
  - Wrapped safe read-only Azure AI Search queries and embedding generation in `AzureAiSearchPolicyRetriever` with `ResiliencePolicy`, returning safe empty policy results (`Enumerable.Empty<PolicySearchResultDto>()`) on outage without falling back to `SyntheticPolicyRetriever`.
  - Preserved strict production rule: Azure AI Search outages must NEVER fall back to `SyntheticPolicyRetriever` in real application paths.
  - Guaranteed that consequential write operations (`save_draft`, officer decisions, document overrides) carry NO blind retries, preventing duplicate write side-effects and preserving state idempotency.
  - Added unit and end-to-end tests (`ResilienceTests.cs`) covering caller cancellation, transient timeout retries, circuit breaker isolation, degraded LLM fallbacks, and safe search empty returns.
  - Verified **126/126 tests passing** across all 6 test projects.

## [Step 10.2 Structured Telemetry & Token Tracking Passed] - 2026-09-15

### Added & Verified
- **Structured Telemetry, Token Tracking & Correlation IDs (`Loan.Application.Abstractions`, `Loan.Infrastructure.Telemetry`, `Loan.Web.Middleware`)**:
  - Implemented `CorrelationContext` (`AsyncLocal<string>`) and `CorrelationIdMiddleware` for generating and propagating `X-Correlation-ID` across HTTP request boundaries and asynchronous call chains.
  - Implemented `ITelemetryCollector` and `InMemoryTelemetryCollector` capturing structured, measurable request metrics (`RequestTelemetry`).
  - Integrated token usage tracking (`PromptTokens`, `CompletionTokens`, `TotalTokens`) in `AzureOpenAIChatModel` (consuming OpenAI SDK Usage API) and `SyntheticChatModel`.
  - Captured stopwatch latencies for LLM completions, RAG searches (`AzureAiSearchPolicyRetriever`, `SyntheticPolicyRetriever`), retrieval hit counts, and specialist agent stages (`DocumentAnalysis`, `EligibilityAnalysis`, `ComplianceReview`, `OrchestratorSynthesis`).
  - Captured MCP tool-call counts, error categories, routing distributions, and officer decisions.
  - Exposed `GET /health/telemetry` endpoint returning structured request telemetry JSON.
  - Ensured PII and credential safety across all telemetry logging via `PiiMasker`.
  - Added unit & integration test suite (`StructuredTelemetryTests.cs`) and verified **119/119 tests passing**.

## [Step 10.1 Health Checks Passed] - 2026-09-15

### Added & Verified
- **Health Checks & Dependency Probing (`Loan.Web.Controllers`, `Loan.EndToEndTests`)**:
  - Implemented `HealthController.cs` providing three health check endpoints:
    - `GET /health` (Process Liveness): Instant response, zero external network calls, returns HTTP 200 `Healthy`.
    - `GET /health/ready` (Core Readiness): Verifies SQL Server database connectivity with a bounded 3-second cancellation timeout. Returns HTTP 200 `Connected` or HTTP 503 `Unavailable`.
    - `GET /health/details` (Detailed Dependency Breakdown): Exposes granular status for SQL Server, Azure OpenAI, and Azure AI Search. Unconfigured/failing AI dependencies return overall status `Degraded` (HTTP 200) without crashing process liveness or failing core SQL readiness.
  - Implemented strongly typed `HealthStatusResponse` and `DependencyHealth` DTOs.
  - Implemented unit and end-to-end integration tests in `HealthControllerTests.cs` validating healthy, unhealthy, degraded, timeout, and cancellation behaviors.
- Total tests passing: **114/114 tests passing** across all 6 test projects.

## [Slice 9 Release Gate Passed] - 2026-09-14


### Added & Verified
- **Security, Privacy & Prompt Refusal Suite (`Loan.Application.Common`, `Loan.PromptTests`)**:
  - Implemented `PiiMasker.cs` utility redacting SSNs (`***-**-1234`), Bank Account Numbers (`******1234`), and Email Addresses before forwarding prompts or persisting log output.
  - Implemented `PromptInjectionGuard.cs` for scanning user inputs and candidate document snippets for injection patterns (`"SYSTEM OVERRIDE"`, `"IGNORE PREVIOUS INSTRUCTIONS"`, `"PRINT SYSTEM PROMPT"`, `"SET STATUS APPROVED"`).
  - Integrated `PromptInjectionGuard` into `AskProductQuestionQueryHandler.cs`, short-circuiting injection attempts with standardized security refusal message (*"Refusal: The request contains unauthorized instructions attempting to override system governance or policy rules..."*).
  - Added unit test suite `PiiMaskerTests.cs` verifying SSN, account number, and email redaction logic.
  - Added unit test suite `CrossApplicationIsolationTests.cs` verifying server-side tenant scoping security across MCP tools and document field override commands.
  - Expanded `PromptEvaluationDataset.cs` in `Loan.PromptTests` with 4 new security refusal prompt test cases (`PROMPT-004` through `PROMPT-007`).
- Total tests passing: **106/106 tests passing** across all 6 test projects.

## [Slice 8 Release Gate Passed] - 2026-09-14

### Added & Verified
- **Officer Review, Decision Enforcement, Audit Trail & MVC Streaming Integration (`Loan.Web.Controllers`, `Loan.Domain`)**:
  - Implemented `StreamRecommendationDraft` Server-Sent Events (SSE) streaming endpoint in `OfficerController.cs` for live multi-agent draft progress monitoring (`text/event-stream`).
  - Enforced **BR-07 Loan Officer Exclusivity**: System agents and LLMs produce status `DraftPreparedBySystem` only; final status transitions (`Approved`, `Rejected`, `InformationRequested`) require authorized Loan Officer execution with mandatory decision notes.
  - Built interactive Officer Review UI in `Officer/Review.cshtml` with facts & ratio display, AI safety non-approval disclaimer, grounded policy citations, live SSE streaming logs, and decision submission form.
  - Rendered append-only immutable audit trail timeline in `Review.cshtml` tracking `DraftPrepared`, `FieldConfirmed`, `FieldOverridden`, `OfficerApproved`, `OfficerRejected`, and `ReturnedForInfo` events.
  - Added unit and integration tests in `OfficerDecisionHandlerTests.cs` and `WebRoutesEndToEndTests.cs` verifying decision transitions, rejection/return paths, invalid state rejection, and audit trail generation.
- Total tests passing: **98/98 tests passing** across all 6 test projects.

## [Slice 7 Release Gate Passed] - 2026-09-14

### Added & Verified
- **Bounded Multi-Agent Specialist Framework & Orchestration (`Loan.Application.Agents`)**:
  - Implemented `DocumentAnalysisAgent` for evaluating candidate extracted fields, document health, and low-confidence flags ($<0.85$).
  - Implemented `EligibilityAnalysisAgent` for explaining financial risk factors. Crucially consumes immutable `EligibilityIndicators` computed by pure C# in `Loan.Domain` without LLM numerical ratio recalculation.
  - Implemented `ComplianceReviewAgent` for RAG policy searches via `IPolicyRetriever`, generating grounded citations (`DocumentTitle`, `PolicyVersion`, `SectionOrPage`, `Excerpt`).
  - Implemented `RecommendationOrchestratorAgent` synthesizing specialist outputs into a `RecommendationDraft`. Enforces deterministic system overrides for `RoutingState` (`PendingInformation`, `ManualReview`, `ReadyForOfficerReview`) and `RiskScore` (0.15 - 0.85) derived directly from `EligibilityIndicators.Status`.
  - Enforced strict non-approval safety guarantees: Orchestrator agent CANNOT create `Approve` or `Reject` decisions, price loans, or disburse funds (**BR-07** - Loan Officer exclusivity for final status decisions).
  - Saved recommendations via `SaveRecommendationDraftCommandHandler` in `DraftPreparedBySystem` status with full audit logging.
  - Added `MultiAgentOrchestrationTests.cs` verifying agent boundaries, immutable domain indicators, routing state overrides, and end-to-end multi-agent orchestration across all 12 synthetic scenarios.
- Total tests passing: **81/81 tests passing** across all 6 test projects.

## [Slice 6 Release Gate Passed] - 2026-09-14

### Added & Verified
- **Deterministic DTI & Eligibility Domain Engine (`Loan.Domain.Eligibility`)**:
  - Enhanced `EligibilityCalculator.cs` enforcing pure C# deterministic financial calculations without LLM involvement (**BR-01**, **BR-03**).
  - Implemented zero/negative income guard (`income <= 0 => DebtToIncomeRatio = null, IsDtiEligible = false, Status = Ineligible`) eliminating false 1.0 (100%) ratio representations.
  - Implemented product-specific LTV applicability (`RequiresPropertyValuation` flag in `ProductRules`). For unsecured Personal Loans (`LOAN-PERSONAL` v2.0), `LoanToValueRatio = null` and `IsLtvEligible = true`. For Mortgages (`MORTGAGE-STD` v1.2), zero property value returns `LoanToValueRatio = null` and `Status = Ineligible`.
  - Implemented explicit 4-state status precedence: `PendingInformation` (unverified mandatory evidence) -> `Ineligible` (invalid inputs / hard rule failure) -> `ReferToHuman` (DTI/LTV policy exception eligible for manual officer review) -> `Eligible` (all rules pass).
  - Verified fact precedence (`ApplicantFacts.EffectiveMonthlyIncome` & `EffectiveCreditScore`) prioritizing verified values over stated facts (**BR-02**).
  - Reconciled `ProductRules` factory defaults with indexed RAG markdown policy documents (`DOC-PERSONAL-V2` v2.0 & `DOC-MORTGAGE-V12` v1.2).
  - Added unit test suite `DeterministicEligibilityEngineTests.cs` (exact boundary thresholds: DTI=43.0%, LTV=80.0%, Credit=640; zero income/property guards; precedence).
  - Added integration scenario runner `SyntheticApplicationScenarioTests.cs` validating all 12 canonical synthetic application scenarios (`SYN-888777` through `SYN-000000`).
- Total tests passing: **76/76 tests passing** across all 6 test projects.

## [Slice 5 Release Gate Passed] - 2026-09-14

### Added & Verified
- **Typed Synthetic Verification Readers & MCP Server**:
  - Implemented typed synthetic readers (`IIdentityReader`, `IIncomeReader`, `ICreditReader`) in `SyntheticVerificationServices.cs` covering 12 synthetic application records with explicit unverified status handling and `CancellationToken` support.
  - Implemented `SaveRecommendationDraftCommand` and handler in `SaveRecommendationDraftCommand.cs` enforcing `Draft` status creation (`DraftPreparedBySystem`), authorized actor role check (`SystemWorker`, `Officer`), audit trail recording, and prohibiting final `Approve` or `Reject` decisions.
  - Implemented `McpToolServer` in `src/Loan.Infrastructure/MCP/McpToolServer.cs` with JSON-RPC 2.0 dispatch, Model Context Protocol spec `2024-11-05` compliance, and 5 application-scoped tools: `get_identity_status`, `get_income_verification`, `get_credit_score`, `search_policy`, and `save_draft`.
  - Enforced server-side `ApplicationId` + `SyntheticId` scoping security; cross-application synthetic identity requests are denied server-side with distinct `CrossApplicationMismatch` error responses.
  - Implemented remote Streamable HTTP endpoint `McpController` (`POST /api/mcp`) reading actor context headers (`X-Actor-Id`, `X-Actor-Role`).
  - Added comprehensive test suites: `SyntheticVerificationServicesTests`, `McpApplicationScopingTests`, `McpSaveDraftTests`, and updated `McpContractTests`.
- Total tests passing: **53/53 tests passing** across all test projects.

## [Slice 4 Release Gate Passed] - 2026-09-14

### Added & Verified
- **Synthetic Document Upload & Candidate Field Extraction Pipeline**:
  - Implemented `IDocumentStorageService` abstraction and `LocalFileDocumentStorageService` streaming uploaded files to `App_Data/Uploads/{ApplicationId}/` using SHA-256 hash generation and sanitized GUID storage references. Raw file bytes are isolated outside EF Core domain aggregates.
  - Implemented `DocumentUploadValidator` for file security (allowed extensions: `.pdf`, `.png`, `.jpg`, `.jpeg`, `.txt`, `.csv`, `.json`, `.md`; MIME type verification, 10MB maximum file size limit, and path traversal rejection `..`).
  - Enhanced `SyntheticDocumentExtractor` supporting 5 document categories (Payslip/Paystub, W-2, Bank Statement, Driver License/Passport, Tax Return), deterministic synthetic extraction examples, low-confidence edge case samples ($<0.85$), and sensitive identifier masking (SSNs formatted as `***-**-6789`).
  - Implemented `UploadAndExtractDocumentCommand` and `ConfirmOrOverrideExtractedFieldsCommand` with role-based authorization (Applicant can confirm their own facts, Officer can override, Compliance read-only rejected) and strict application ID isolation.
  - Implemented safe audit logging via `FieldOverrideAuditEntry` tracking application ID, field, document ID, actor, role, timestamp, action ("Confirm" vs "Override"), reason, and correlation ID without raw PII values.
  - Added EF Core Migration `AddDocumentExtraction` and JSON column mappings (`DocumentsJson`, `DocumentAuditTrailJson`).
  - Added `DocumentStorageTests`, `DocumentExtractionTests`, `DocumentAuthorizationTests`, and `DocumentPersistenceIntegrationTests`.
- Total tests passing: **44/44 tests passing** across 6 test projects (Domain: 11, Application: 11, Prompt: 4, Contract: 3, E2E: 4, Integration: 11).

## [Slice 3 Release Gate Passed] - 2026-09-14

### Added & Verified
- **Azure AI Search & Hybrid Vector RAG (`Azure.Search.Documents` v11.6.0)**:
  - Installed official Azure SDK package `Azure.Search.Documents` (v11.6.0) in `Loan.Infrastructure.csproj`.
  - Created 8 synthetic versioned policy markdown documents in `src/Loan.Infrastructure/Search/SeedPolicies/` (24 policy document chunks).
  - Built `PolicyIndexDocument` search schema with HNSW vector index profile (1536 dimensions, Cosine metric, `ContentVector`).
  - Built `PolicyIndexer` for parsing BOM-safe YAML frontmatter metadata and section chunking, generating embeddings via Azure OpenAI `text-embedding-3-small`, and uploading documents into Azure AI Search `loan-policies-index` via idempotent `MergeOrUploadDocumentsAsync`.
  - Built `AzureAiSearchPolicyRetriever` for hybrid keyword + vector search, metadata filtering (`productId`, `policyVersion`), active version filtering (`isActive eq true`), hybrid RRF score thresholding (`score >= 0.0165`), and citation metadata generation (`CitationDto`).
  - Verified **Zero Silent Runtime Fallbacks**: missing configuration throws an explicit actionable `InvalidOperationException`.
  - Verified **Active Policy Version Selection**: Personal Loan v2.0 (`EffectiveFrom: 2024-01-01`, active) automatically selected over expired Personal Loan v1.0 (`EffectiveTo: 2023-12-31`).
  - Verified **Idempotency & Resilience**: `PolicyIndexingWorker` handles repeated application starts without chunk duplication; index search outages log warnings gracefully without crashing application startup.
  - Verified **Insufficient Evidence Safety**: Irrelevant policy topics return 0 search results, producing an explicit refusal message from the AI assistant without hallucination.
  - Verified **Secrets Isolation**: Zero Azure Search or OpenAI credentials committed in git repository.
- Total tests passing: **30/30 tests passing** across 6 test projects (Domain: 11, Application: 5, Prompt: 4, Contract: 3, E2E: 4, Integration: 3).

## [Slice 2] - 2026-09-14

### Added
- **Azure OpenAI Integration (`Azure.AI.OpenAI` v2.1.0)**:
  - Installed official Microsoft Azure SDK `Azure.AI.OpenAI` (v2.1.0 stable GA release) in `Loan.Infrastructure.csproj`.
  - Implemented `AzureOpenAIChatModel` in `src/Loan.Infrastructure/AzureOpenAI/AzureOpenAIChatModel.cs` implementing `IChatModel`.
  - Registered `AzureOpenAIChatModel` in DI container (`Infrastructure.DependencyInjection.cs`).
  - Implemented explicit configuration startup validation; throws clear `InvalidOperationException` with setup guidance if endpoint/key are unconfigured.
  - Configured local User Secrets (`AzureOpenAI:Endpoint`, `AzureOpenAI:ApiKey`, `AzureOpenAI:DeploymentName`, `AzureOpenAI:EmbeddingDeploymentName`).
  - Added safe configuration template in `appsettings.json`.
  - Added `AzureOpenAiIntegrationTests.cs` in `Loan.IntegrationTests` verifying live completion against Azure AI Foundry deployment (`gpt-4o`).
- Total tests passing: **34/34 tests passing**.

## [Slice 1 Release Gate Passed] - 2026-09-14

### Added / Verified
- **EF Core Migrations & SQL Server Persistence**:
  - Reproducible EF Core Migration `20260914103301_InitialCreate.cs` generated and applied to local SQL Server.
  - Startup migration configured to be development-only (`Database:AutoMigrateOnStartup`).
  - Verified physical persistence in SQL Server (`LoanAssistantDb`) via `sqlcmd` and SSMS inspection (`LoanApplications` & `Recommendations` tables populated with seeded data).
- **Secrets Security & Configuration Isolation**:
  - `appsettings.json` updated with safe template connection string without credentials (`Server=YOUR_SERVER;Database=LoanAssistantDb...`).
  - User Secrets store configured for local development (`dotnet user-secrets set ConnectionStrings:DefaultConnection ...`).
