# Change Log

All notable changes to the Loan Application & Compliance Review Assistant project will be documented in this file.

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
