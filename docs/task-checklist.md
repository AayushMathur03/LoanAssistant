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

### Slice 6: Deterministic DTI & Eligibility Domain Rules
- [ ] Expand eligibility calculator tests & domain rules.
- [ ] Validate deterministic calculations against all 12 synthetic application scenarios.

### Slice 7: Bounded Multi-Agent Orchestration
- [ ] Implement Document Agent, Eligibility Agent, and Compliance Agent.
- [ ] Orchestrate schema-valid `RecommendationDraft` generation.

### Slice 8: Officer Review & Audit Trail
- [ ] Wire controlled status transitions for officer approval/rejection/return.
- [ ] Display audit history timeline in UI.

### Slice 9: Security, Privacy & Prompt Refusal
- [ ] Add sensitive data masking.
- [ ] Expand prompt injection refusal test suite.

### Slice 10: Telemetry, Evaluation Runner & Resilience
- [ ] Implement 20 evaluation prompt runner & health metrics.
