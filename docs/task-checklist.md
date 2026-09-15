# Task Checklist

## Vertical Slices Progress

### Slice 1: SQL Server + EF Core Application Persistence
- [x] Inspect existing repository and document architecture, fake services, and starter data.
- [x] Add EF Core SQL Server packages (`Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`) to `Loan.Infrastructure`.
- [x] Create `LoanDbContext` in `Loan.Infrastructure/Persistence/DbContext/LoanDbContext.cs` with JSON serialization mappers.
- [x] Implement `SqlLoanApplicationRepository` and `SqlRecommendationRepository` in `Loan.Infrastructure/Persistence/Repositories/SqlRepositories.cs`.
- [x] Configure SQL Server connection string in `appsettings.json` and register `LoanDbContext` in `Program.cs`.
- [x] Add automatic startup DB creation (`EnsureCreatedAsync`) and data seeding in `Program.cs`.
- [x] Write `SqlPersistenceIntegrationTests` in `tests/Loan.IntegrationTests`.
- [x] Verify all test suites pass (32/32 tests passing).

### Slice 2: Azure OpenAI Chat Endpoint
- [x] Install `Azure.AI.OpenAI` (v2.1.0 stable GA release) package in `Loan.Infrastructure.csproj`.
- [x] Implement `AzureOpenAIChatModel` adapter in `Loan.Infrastructure/AzureOpenAI/AzureOpenAIChatModel.cs` implementing `IChatModel`.
- [x] Configure endpoint, API key, deployment name, and embedding deployment name via ASP.NET Core User Secrets.
- [x] Add safe configuration template section in `appsettings.json`.
- [x] Register `AzureOpenAIChatModel` as `IChatModel` in `Infrastructure.DependencyInjection.cs`.
- [x] Add `AzureOpenAiIntegrationTests.cs` testing live completions against Azure AI Foundry `gpt-4o` deployment.
- [x] Verify live response completion & error handling (34/34 tests passing).

### Slice 3: Synthetic Product/Compliance Documents & Azure AI Search RAG
- [x] Install `Azure.Search.Documents` (v11.6.0 stable GA release) package in `Loan.Infrastructure.csproj`.
- [x] Create 8 versioned synthetic policy markdown documents in `src/Loan.Infrastructure/Search/SeedPolicies/`.
- [x] Implement Azure AI Search index schema (`PolicyIndexDocument`) and document ingestion (`PolicyIndexer`).
- [x] Wire `PolicyIndexingWorker` to automate index creation & 1536-dim embedding generation (`text-embedding-3-small`).
- [x] Implement `AzureAiSearchPolicyRetriever` with hybrid search, metadata filtering (`productId`, `policyVersion`), and citation metadata.
- [x] Add `AzureAiSearchIntegrationTests.cs` testing live index synchronization, vector embedding generation, and hybrid retrieval.
- [x] Verify full solution test suite passing (30/30 tests passing, 0 failing across all projects).
- [x] Complete Slice 3 release gate review (Runtime fallback, Effective version selection, Indexing worker idempotency, RAG end-to-end flow, Insufficient evidence refusal, Citations, 8 seed docs / 24 chunks).

### Slice 4: Synthetic Document Upload & Extraction Pipeline
- [x] Implement `IDocumentStorageService` and `LocalFileDocumentStorageService` for local stream storage (`App_Data/Uploads`).
- [x] Implement `DocumentUploadValidator` for file security (allowed extensions, MIME validation, 10MB file limit, path traversal rejection).
- [x] Enhance `SyntheticDocumentExtractor` supporting 5 document types (Paystub, W-2, Bank Statement, Driver License/Passport, Tax Return), sensitive SSN masking (`***-**-6789`), and low-confidence edge cases ($<0.85$).
- [x] Implement `UploadAndExtractDocumentCommand` and `ConfirmOrOverrideExtractedFieldsCommand` with role-based authorization (Applicant/Officer allowed, Compliance read-only rejection) and application isolation.
- [x] Add EF Core JSON column mappings (`DocumentsJson`, `DocumentAuditTrailJson`) and migration `AddDocumentExtraction`.
- [x] Implement `DocumentStorageTests`, `DocumentExtractionTests`, `DocumentAuthorizationTests`, and `DocumentPersistenceIntegrationTests`.
- [x] Verify full solution test suite passing (44/44 tests passing).

### Slice 5: Typed Verification Tools & MCP Server
- [x] Implement typed synthetic readers (`IIdentityReader`, `IIncomeReader`, `ICreditReader`) with 12 synthetic application records and explicit unverified status handling (`SyntheticVerificationServices.cs`).
- [x] Implement `SaveRecommendationDraftCommand` enforcing `Draft` status creation (`DraftPreparedBySystem`), role authorization (`SystemWorker`, `Officer`), audit logging, and prohibiting final Approve/Reject decisions.
- [x] Implement `McpToolServer` with JSON-RPC 2.0 dispatch, standard MCP protocol (`2024-11-05`), JSON schema definitions for 5 tools, and server-side `ApplicationId` + `SyntheticId` scoping security.
- [x] Implement `McpController` providing remote Streamable HTTP endpoint (`POST /api/mcp`) reading actor headers (`X-Actor-Id`, `X-Actor-Role`).
- [x] Register Slice 5 verification readers, draft handler, and MCP server in `DependencyInjection.cs`.
- [x] Add `SyntheticVerificationServicesTests`, `McpApplicationScopingTests`, `McpSaveDraftTests`, and update `McpContractTests`.
- [x] Verify full solution test suite passing (53/53 tests passing across all projects).

### Slice 6: Deterministic DTI & Eligibility Domain Engine
- [x] Update `ProductRules.cs` adding `RequiresPropertyValuation` property and reconciling factory defaults with RAG policies (`MORTGAGE-STD` v1.2, `LOAN-PERSONAL` v2.0).
- [x] Make `DebtToIncomeRatio` and `LoanToValueRatio` nullable decimals in `EligibilityIndicators` and DTOs to represent zero/invalid income and non-applicable property valuations cleanly.
- [x] Implement strict 4-state status precedence in `EligibilityCalculator.cs`: `PendingInformation` -> `Ineligible` -> `ReferToHuman` -> `Eligible`.
- [x] Add zero/negative income guard (`income <= 0 => DTI = null, IsDtiEligible = false, Status = Ineligible`) and zero/negative property value guard (`propertyValue <= 0 => LTV = null, IsLtvEligible = false, Status = Ineligible`).
- [x] Verify verified-over-stated fact precedence (`ApplicantFacts.EffectiveMonthlyIncome` & `EffectiveCreditScore`) with zero LLM involvement.
- [x] Add `DeterministicEligibilityEngineTests.cs` covering exact boundary thresholds, edge cases, precedence, and status semantics.
- [x] Add `SyntheticApplicationScenarioTests.cs` validating all 12 canonical synthetic application scenarios (`SYN-888777` through `SYN-000000`).
- [x] Verify full solution test suite passing (76/76 tests passing).

### Slice 7: Bounded Multi-Agent Specialist Framework & Orchestration
- [x] Implement `AgentModels.cs` and versioned `AgentPrompts.cs` (`AgentPrompts_v1.0.cs`).
- [x] Implement `DocumentAnalysisAgent` for inspecting document metadata, extracted field confidence scores ($<0.85$), and unconfirmed status.
- [x] Implement `EligibilityAnalysisAgent` for explaining financial factors by consuming immutable C# `EligibilityIndicators` from `Loan.Domain` without LLM ratio recalculation.
- [x] Implement `ComplianceReviewAgent` for RAG policy searches via `IPolicyRetriever`, grounding assertions in versioned citations.
- [x] Implement `RecommendationOrchestratorAgent` for synthesizing specialist outputs, enforcing deterministic `RoutingState` and `RiskScore` overrides, and saving draft recommendations via `SaveRecommendationDraftCommandHandler`.
- [x] Register Slice 7 specialist agents and orchestrator in `DependencyInjection.cs`.
- [x] Add `MultiAgentOrchestrationTests.cs` testing agent boundaries, immutability of domain indicators, routing state overrides, and end-to-end multi-agent orchestration across all 12 synthetic scenarios.
- [x] Verify full solution test suite passing (81/81 tests passing).

### Slice 8: Officer Review, Decision Enforcement, Audit Trail & MVC Streaming Integration
- [x] Implement `StreamRecommendationDraft` Server-Sent Events (SSE) streaming endpoint in `OfficerController.cs` for live multi-agent draft progress monitoring.
- [x] Enforce **BR-07 Loan Officer Exclusivity**: System agents and LLMs produce status `DraftPreparedBySystem` only; final status transitions (`Approved`, `Rejected`, `InformationRequested`) require authorized Loan Officer execution with mandatory decision notes.
- [x] Build interactive Officer Review UI in `Officer/Review.cshtml` with facts & ratio display, AI safety non-approval disclaimer, grounded policy citations, and live SSE streaming logs.
- [x] Render append-only immutable audit trail timeline in `Review.cshtml` tracking `DraftPrepared`, `FieldConfirmed`, `FieldOverridden`, `OfficerApproved`, `OfficerRejected`, and `ReturnedForInfo` events.
- [x] Add unit and integration tests in `OfficerDecisionHandlerTests.cs` and `WebRoutesEndToEndTests.cs` verifying decision transitions, invalid state rejection, and audit trail generation.
- [x] Verify full solution test suite passing (98/98 tests passing across all 6 test projects).

### Slice 9: Security, Privacy & Prompt Refusal Suite
- [x] Implement `PiiMasker.cs` for redacting SSNs (`***-**-1234`), Account Numbers (`******1234`), and Email Addresses before forwarding prompts or persisting logs.
- [x] Implement `PromptInjectionGuard.cs` scanning queries for jailbreak, override, and system prompt extraction attacks (`"SYSTEM OVERRIDE"`, `"IGNORE PREVIOUS INSTRUCTIONS"`, `"PRINT SYSTEM PROMPT"`).
- [x] Integrate prompt injection defense in `AskProductQuestionQueryHandler.cs` short-circuiting with standardized security refusal message.
- [x] Verify cross-application tenant isolation server-side in `McpToolServer.cs` and document field override commands.
- [x] Add unit & prompt test suites (`PiiMaskerTests.cs`, `CrossApplicationIsolationTests.cs`, expanded `PromptEvaluationDataset.cs`).
- [x] Verify full solution test suite passing (106/106 tests passing across all 6 test projects).

### Slice 10: Telemetry, Evaluation Runner & Resilience
- [x] **Step 10.1: Health Checks**:
  - Implemented `/health` process liveness endpoint (fast response, zero external network calls).
  - Implemented `/health/ready` core readiness endpoint with SQL Server connectivity verification and bounded 3-second timeout.
  - Implemented `/health/details` detailed dependency breakdown exposing status for SQL Server, Azure OpenAI, and Azure AI Search (AI dependency failures mark overall status as `Degraded` HTTP 200 without failing process liveness).
  - Added `HealthControllerTests.cs` verifying healthy/unhealthy/degraded states, timeout handling, and structured response shapes.
  - Verified full test suite passing (114/114 tests passing across all 6 test projects).
- [ ] Step 10.2: Structured Telemetry & Token Tracking
- [ ] Step 10.3: Resilience & Transient Policy Controls
- [ ] Step 10.4: 20-Prompt Evaluation Suite & Runner
- [ ] Step 10.5: Production Database Migration Strategy & Azure Configuration
- [ ] Step 10.6: Rollback Documentation & Final Demonstration Evidence

