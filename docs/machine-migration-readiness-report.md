# LoanAssistant Machine Migration Readiness Report

> **Audit Type**: Pre-Migration Read-Only Readiness Audit  
> **Source Machine**: Personal Development Laptop (Windows 10/11 x64, .NET SDK 10.0.301)  
> **Target Machine**: Company Development Laptop  
> **Audit Date**: 2026-09-17  
> **Repository Commit**: `e39353f` (Clean working tree, 0 uncommitted changes, synchronized with `origin/main`)  
> **Solution File**: [LoanAssistant.slnx](file:///c:/Development/LoanAssistant/LoanAssistant.slnx) (11 projects: 5 source, 6 test)  
> **Classification Tagging**: `VERIFIED`, `INFERRED`, `UNKNOWN`, `REQUIRES USER ACTION`  

---

## 1. Executive Summary

- **Audit Status**: `COMPLETE`
- **Project Modified During Audit**: `NO` (Read-only inspection only)
- **Migration Readiness**: `YES — CONDITIONALLY READY (REQUIRES SECRET & TOOL PROVISIONING)`
- **Critical Migration Blockers**: **2**
  1. **User Secrets Provisioning**: Live Azure credentials (OpenAI, AI Search, Blob Storage) exist only in local developer user secrets on the personal laptop and must be re-entered on the company laptop.
  2. **Database Engine Availability**: Default configuration assumes `(localdb)\MSSQLLocalDB`, which is often not installed by default on corporate Windows laptops unless Visual Studio with Data Storage workload is installed.
- **High Risks**: **2**
  1. **Corporate Network / Proxy Firewalls**: Outbound traffic to Azure OpenAI (`.openai.azure.com:443`), Azure AI Search (`.search.windows.net:443`), and Azure Storage (`.core.windows.net:443`) must not be blocked or MITM-intercepted by enterprise proxy firewalls.
  2. **Git / GitHub Enterprise Access**: Access to GitHub repository `AayushMathur03/LoanAssistant` must be permitted through corporate VPN/SSO.
- **User Actions Required Before Migration**: **5** (Documented in Section 30)

---

## 2. Current Git State

- **Status**: `VERIFIED`
- **Active Branch**: `main`
- **Head Commit**: [`e39353f`](https://github.com/AayushMathur03/LoanAssistant/commit/e39353f) (*feat(applicant): implement table view, reverse chronological sorting, and status filtering for My Applications*)
- **Working Tree**: Completely clean (`nothing to commit, working tree clean`)
- **Remote URL**: `https://github.com/AayushMathur03/LoanAssistant.git`
- **Remote Synchronization**: Synchronized with `origin/main` (0 commits ahead, 0 commits behind)
- **Git Tags**: None configured
- **Untracked Files**: `0` (Zero untracked files)
- **Ignored Files Present Locally**:
  - `src/Loan.Web/App_Data/Uploads/APP-2026-001/*.bin` (3 local document binaries; safe to recreate from tracked `SampleDocuments/`)
  - `src/Loan.Web/Loan.Web.csproj.user` (local developer IDE user settings)
  - `.vs/` (Visual Studio local design-time cache)

### Git Migration Status
- **Safe to clone?**: `YES`
- **Missing tracked files**: `None`
- **Important untracked files**: `None`
- **Important ignored files**: `src/Loan.Web/App_Data/Uploads` (can be recreated cleanly on company laptop using tracked synthetic stubs in `src/Loan.Web/SampleDocuments/`)
- **Current checkpoint commit**: `e39353f`
- **Recommended Git checkpoint**: The repository is already committed and pushed to `e39353f`. A recommended pre-migration tag can be created if desired: `git tag pre-migration-checkpoint-20260917`.

---

## 3. Repository Contents

- **Status**: `VERIFIED`
- **Canonical Project Context File**: [context_UPDATED.md](file:///c:/Development/LoanAssistant/context_UPDATED.md) (33,039 bytes)
  - Defines the CapGemini AI Launchpad / GenAI Powered .NET Application Development — Use Case 4 implementation contract.
- **Canonical Implementation Prompt File**: [CLAUDE_IMPLEMENTATION_PROMPT_UPDATED.md](file:///c:/Development/LoanAssistant/CLAUDE_IMPLEMENTATION_PROMPT_UPDATED.md) (44,290 bytes)
  - Defines the prompt instructions, Clean Architecture layer rules, deterministic domain constraints, and safety guidelines.
- **Documentation Directory**: [docs/](file:///c:/Development/LoanAssistant/docs)
  - `phased-implementation-plan-and-tracker.md`: Current implementation phase is **Phase 4 Complete** + Post-Phase Enhancements (Tasks 4, 11, 12, 13, and Applicant Table View complete).
  - `change-log.md`: Detailed chronological record of all changes.
  - `decision-log.md`: ADR-001 through ADR-015 documenting architectural decisions.
  - `local-product-verification.md`: Verification guide for local demo execution.
  - `scenario-demonstration-guide.md`: Step-by-step walkthrough for all 4 canonical personas.
  - `evaluation_report.md` & `evaluation_results.json`: 20-prompt evaluation benchmark results.
  - `task-checklist.md`: Feature verification matrix.
- **Document Stubs / Policy Corpora**:
  - `src/Loan.Infrastructure/Search/SeedPolicies/`: 8 authoritative versioned policy markdown files (`01_DOC-PERSONAL-V1` through `08_DOC-EXCEPTIONS-V1`). All tracked in Git.
  - `src/Loan.Web/SampleDocuments/`: 6 synthetic applicant documents (`Alice_Cooper_Paystub_Verified.txt`, `Alice_Cooper_BankStatement_60Day.txt`, `Alice_Cooper_DriverLicense_Masked.txt`, `Jane_Smith_BankStatement_60Day.txt`, `LowConfidence_Smudged_Paystub.txt`, `Adversarial_Prompt_Injection_Document.txt`). All tracked in Git.

---

## 4. Project Architecture

- **Status**: `VERIFIED`
- **Target Framework**: .NET 8.0 (`net8.0`) across all 11 projects.
- **Architecture Pattern**: Strict Clean Architecture (Domain $\rightarrow$ Application $\rightarrow$ Infrastructure / Web / Workers).

```
+-------------------------------------------------------------------------------+
|                               Loan.Domain                                     |
|  - Value Objects: Money, SSN, TaxId, Address                                  |
|  - Aggregates: LoanApplication, Recommendation                                |
|  - Domain Rules: EligibilityCalculator, ProductRules (Mortgage, Personal, Auto)|
|  - Zero third-party dependencies (No EF, No ASP.NET, No Azure SDKs)           |
+-------------------------------------------------------------------------------+
                                      ^
                                      |
+-------------------------------------------------------------------------------+
|                             Loan.Application                                  |
|  - Abstractions: ILoanApplicationRepository, IChatModel, IPolicyRetriever     |
|  - Specialist Agents: DocumentAnalysis, Eligibility, Compliance, Orchestrator |
|  - CQRS Handlers: Upload, Confirm, Evaluate, SaveDraft, OfficerDecision       |
|  - Security: PiiMasker, PromptInjectionGuard                                  |
+-------------------------------------------------------------------------------+
                                      ^
         +----------------------------+----------------------------+
         |                                                         |
+------------------------------------+   +------------------------------------+
|        Loan.Infrastructure         |   |              Loan.Web              |
|  - EF Core & SqlServer / LocalDB   |   |  - ASP.NET Core MVC (Razor + CSS)  |
|  - Identity (AspNetUsers/Roles)    |   |  - 4 Personas: Applicant, Officer, |
|  - AzureOpenAIClient (gpt-4o)      |   |    Compliance, Admin               |
|  - Azure AI Search (Hybrid RAG)    |   |  - MCP API Controller (/api/mcp)   |
|  - Azure Blob Storage + Fallback   |   |  - Health Endpoints (/health)      |
|  - Semantic Kernel Native Plugins  |   |  - UserSecretsId: LoanAssistant-   |
|  - MCP Tool Server (JSON-RPC 2.0)  |   |    Web-87612345-EF89               |
+------------------------------------+   +------------------------------------+
         ^                                                         ^
         |                                                         |
+------------------------------------+   +------------------------------------+
|            Loan.Workers            |   |               Tests                |
|  - Background Hosted Services      |   |  - Domain.Tests (20 unit tests)    |
|  - PolicyIndexingWorker (AI Search)|   |  - ContractTests (4 tests)         |
|  - DocumentProcessingWorker        |   |  - Application.Tests (59 tests)    |
|  - UserSecretsId: dotnet-Workers   |   |  - EndToEndTests (34 tests)        |
|                                    |   |  - PromptTests (21 tests)          |
|                                    |   |  - IntegrationTests (23 tests)     |
+------------------------------------+   +------------------------------------+
```

### Dependency Graph & References
1. `Loan.Domain`: No project dependencies.
2. `Loan.Application` references: `Loan.Domain`.
3. `Loan.Infrastructure` references: `Loan.Application`, `Loan.Domain`.
   - NuGet Packages: `Azure.AI.OpenAI` (2.1.0), `Azure.Search.Documents` (11.6.0), `Azure.Storage.Blobs` (12.21.0), `Microsoft.SemanticKernel` (1.34.0), `Microsoft.EntityFrameworkCore.SqlServer` (8.0.8), `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (8.0.8).
4. `Loan.Web` references: `Loan.Application`, `Loan.Infrastructure`.
   - NuGet Packages: `Microsoft.EntityFrameworkCore.Design` (8.0.8).
5. `Loan.Workers` references: `Loan.Application`, `Loan.Infrastructure`.
   - NuGet Packages: `Microsoft.Extensions.Hosting` (8.0.1).
6. Test Projects:
   - `Loan.Domain.Tests` references `Loan.Domain`.
   - `Loan.Application.Tests` references `Loan.Application`, `Loan.Domain`, `Loan.Infrastructure`.
   - `Loan.ContractTests` references `Loan.Application`, `Loan.Infrastructure`.
   - `Loan.IntegrationTests` references `Loan.Application`, `Loan.Infrastructure` (includes `Microsoft.EntityFrameworkCore.InMemory`).
   - `Loan.PromptTests` references `Loan.Application`, `Loan.Infrastructure`.
   - `Loan.EndToEndTests` references `Loan.Web`.

---

## 5. Build/Test Baseline

- **Status**: `VERIFIED`
- **Build Command**: `dotnet build LoanAssistant.slnx`
  - **Result**: `Build succeeded. 0 Warning(s), 0 Error(s)`
  - **Output Assemblies**: 11 assemblies built for `net8.0`.
- **Test Command**: `dotnet test LoanAssistant.slnx --no-build`
  - **Test Projects**: 6
  - **Total Tests**: **161**
  - **Passed**: **161** (100% green)
  - **Failed**: **0**
  - **Skipped**: **0**
  - **Summary**:
    - `Loan.Domain.Tests`: 20 passed (35 ms)
    - `Loan.ContractTests`: 4 passed (58 ms)
    - `Loan.Application.Tests`: 59 passed (182 ms)
    - `Loan.EndToEndTests`: 34 passed (5.0 s)
    - `Loan.PromptTests`: 21 passed (1 m 27 s)
    - `Loan.IntegrationTests`: 23 passed (2 m 13 s)

---

## 6. Configuration Inventory

- **Status**: `VERIFIED`
- **Tracked Configuration Files**:
  - `src/Loan.Web/appsettings.json`
  - `src/Loan.Web/appsettings.Development.json`
  - `src/Loan.Workers/appsettings.json`
  - `src/Loan.Workers/appsettings.Development.json`
  - `src/Loan.Web/Properties/launchSettings.json`

### Complete Configuration Keys Mapping

| Key | Category | Default / File Value | Consumed In | Required on Company Laptop |
| :--- | :--- | :--- | :--- | :--- |
| `ConnectionStrings:DefaultConnection` | Database-specific | `Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=LoanAssistantDb;...` | `DependencyInjection.cs` | **YES** |
| `AzureStorage:ConnectionString` | Secret / Azure | `DefaultEndpointsProtocol=https;AccountName=YOUR-STORAGE-ACCOUNT;...` | `AzureBlobDocumentStorageService.cs` | **YES** (or fallback to local) |
| `AzureStorage:ContainerName` | Azure-specific | `loan-documents` | `AzureBlobDocumentStorageService.cs` | **YES** |
| `AzureOpenAI:Endpoint` | Azure-specific | `https://YOUR-RESOURCE-NAME.openai.azure.com/` | `AzureOpenAIChatModel.cs` | **YES** |
| `AzureOpenAI:ApiKey` | Secret | `""` (empty placeholder) | `AzureOpenAIChatModel.cs` | **YES** (for live AI) |
| `AzureOpenAI:DeploymentName` | Azure-specific | `gpt-4o` | `AzureOpenAIChatModel.cs` | **YES** |
| `AzureOpenAI:EmbeddingDeploymentName` | Azure-specific | `text-embedding-3-small` | `AzureAiSearchPolicyRetriever.cs` | **YES** |
| `AzureAISearch:Endpoint` | Azure-specific | Not in appsettings (User Secrets) | `AzureAiSearchPolicyRetriever.cs` | **YES** (for live RAG) |
| `AzureAISearch:ApiKey` | Secret | Not in appsettings (User Secrets) | `AzureAiSearchPolicyRetriever.cs` | **YES** (for live RAG) |
| `AzureAISearch:IndexName` | Azure-specific | `loan-policies-index` | `AzureAiSearchPolicyRetriever.cs` | **YES** |
| `Database:AutoMigrateOnStartup` | Environment-specific | `true` (default in Development) | `Program.cs` | Safe static |
| `Logging:LogLevel:Default` | Safe static | `Information` | Host / Logging | Safe static |
| `Logging:LogLevel:Microsoft.AspNetCore` | Safe static | `Warning` | Host / Logging | Safe static |

---

## 7. User Secrets Inventory

- **Status**: `VERIFIED`
- **Secrets Policy**: Secret values are strictly `[REDACTED — SECRET EXISTS]`.
- **Project 1: `Loan.Web`** (`UserSecretsId`: `LoanAssistant-Web-87612345-EF89`)

| Configuration Key | Purpose | Required on Company Laptop | Where Consumed | Value Status |
| :--- | :--- | :--- | :--- | :--- |
| `ConnectionStrings:DefaultConnection` | SQL Server / LocalDB connection string | **YES** | `DependencyInjection.cs` (LoanDbContext) | `[REDACTED — SECRET EXISTS]` |
| `AzureOpenAI:Endpoint` | Azure OpenAI resource endpoint URL | **YES** | `AzureOpenAIChatModel.cs` | `[REDACTED — SECRET EXISTS]` |
| `AzureOpenAI:ApiKey` | Azure OpenAI API access key | **YES** | `AzureOpenAIChatModel.cs` | `[REDACTED — SECRET EXISTS]` |
| `AzureOpenAI:DeploymentName` | Chat completion model deployment (`gpt-4o`) | **YES** | `AzureOpenAIChatModel.cs` | `[REDACTED — SECRET EXISTS]` |
| `AzureOpenAI:EmbeddingDeploymentName` | Text embeddings model deployment (`text-embedding-3-small`) | **YES** | `AzureAiSearchPolicyRetriever.cs`, `PolicyIndexer.cs` | `[REDACTED — SECRET EXISTS]` |
| `AzureAISearch:Endpoint` | Azure AI Search service URL | **YES** | `AzureAiSearchPolicyRetriever.cs`, `PolicyIndexer.cs` | `[REDACTED — SECRET EXISTS]` |
| `AzureAISearch:ApiKey` | Azure AI Search admin/query key | **YES** | `AzureAiSearchPolicyRetriever.cs`, `PolicyIndexer.cs` | `[REDACTED — SECRET EXISTS]` |
| `AzureAISearch:IndexName` | Search index identifier (`loan-policies-index`) | **YES** | `AzureAiSearchPolicyRetriever.cs`, `PolicyIndexer.cs` | `[REDACTED — SECRET EXISTS]` |
| `AzureStorage:ConnectionString` | Azure Blob Storage account connection string | **YES** (for live Blob storage) | `AzureBlobDocumentStorageService.cs` | `[REDACTED — SECRET EXISTS]` |
| `AzureStorage:ContainerName` | Blob container name (`loan-documents`) | **YES** | `AzureBlobDocumentStorageService.cs` | `[REDACTED — SECRET EXISTS]` |

- **Project 2: `Loan.Workers`** (`UserSecretsId`: `dotnet-Loan.Workers-b3fbceff-f939-4127-8f0f-a246c9ebcec2`)
  - **Status**: `No secrets configured for this application`. The worker currently shares assembly DI configuration or runs in headless mode.

---

## 8. Environment Variables

- **Status**: `VERIFIED`
- **Result**: **Zero runtime configuration comes from machine environment variables.**
- No variables named `AZURE_*`, `OPENAI_*`, `SEARCH_*`, `STORAGE_*`, `SQL_*`, or `LOAN_*` were found active.
- Runtime configuration relies 100% on standard .NET hierarchy: `appsettings.json` $\rightarrow$ `appsettings.Development.json` $\rightarrow$ .NET User Secrets.

---

## 9. Database Architecture

- **Status**: `VERIFIED`
- **Database Engine**: Microsoft SQL Server / LocalDB (`(localdb)\MSSQLLocalDB`).
- **Database Name**: `LoanAssistantDb`.
- **Database Context**: `LoanDbContext` in `Loan.Infrastructure.Persistence.DbContext`.
- **ORM / Data Access**: Entity Framework Core 8.0.8 (`Microsoft.EntityFrameworkCore.SqlServer`).
- **Schema Synchronization**:
  - Automatically migrates on startup in `Development` environment via `EnsureDatabaseAndSeedAsync(app)` in `Program.cs`.
- **Current Live Local Database State**:
  - `__EFMigrationsHistory`: 3 migrations applied
  - `AspNetRoles`: 4 roles (`Applicant`, `LoanOfficer`, `ComplianceReviewer`, `Administrator`)
  - `AspNetUsers`: 4 users (`applicant@apex.local`, `officer@apex.local`, `compliance@apex.local`, `admin@apex.local`)
  - `AspNetUserRoles`: 4 user-to-role mappings
  - `LoanApplications`: 15 rows (4 canonical seed applications + custom user-created test loans)
  - `Recommendations`: 19 rows (system drafts and officer decisions)

---

## 10. Database Migration Strategy

- **Status**: `VERIFIED & RECOMMENDED`

### Recommended Strategy: **OPTION A (Fresh Database + Automatic EF Migrations + Deterministic Seed Data)**
- **Why this strategy is optimal for this repository**:
  1. `EnsureDatabaseAndSeedAsync()` in [Program.cs](file:///c:/Development/LoanAssistant/src/Loan.Web/Program.cs#L87-L103) is completely automated and idempotent.
  2. On first run of `dotnet run`, EF Core automatically applies all 3 migrations (`ApplyInfrastructureMigrationsAsync`).
  3. Identity roles and users are seeded deterministically with predefined credentials (`SeedIdentityUsersAndRolesAsync`).
  4. The 4 canonical demo applications (`APP-2026-001` through `APP-2026-004`) covering all evaluation scenarios are automatically seeded with verified documents, indicator calculations, and drafts (`DemoDataSeeder.SeedDemoApplicationsAsync`).
  5. Starting fresh eliminates ephemeral testing rows and guarantees that the company laptop begins from a clean, certified state.

### Optional Fallback: **OPTION B (Database Backup / Export)**
- If the user explicitly wishes to preserve specific ad-hoc loans created during development, a SQL Server `.bak` backup file can be exported from `(localdb)\MSSQLLocalDB` using:
  ```powershell
  sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "BACKUP DATABASE [LoanAssistantDb] TO DISK = 'C:\temp\LoanAssistantDb_Backup.bak'"
  ```

---

## 11. EF Migration Inventory

- **Status**: `VERIFIED`
- **Assembly**: `Loan.Infrastructure.dll`

| Order | Migration Identifier | Purpose | Created / Applied |
| :--- | :--- | :--- | :--- |
| 1 | `20260914103301_InitialCreate` | Creates core `LoanApplications` and `Recommendations` tables | Applied (EF 8.0.8) |
| 2 | `20260914135830_AddDocumentExtraction` | Adds document metadata, extracted fields JSON, and audit trails | Applied (EF 8.0.8) |
| 3 | `20260915182307_AddIdentityTables` | Adds ASP.NET Core Identity tables (`AspNetUsers`, `AspNetRoles`, etc.) | Applied (EF 8.0.8) |

---

## 12. Identity/Auth Migration

- **Status**: `VERIFIED`
- **Framework**: ASP.NET Core Identity with EF Core store (`LoanDbContext`).
- **Authentication Scheme**: Cookie authentication (`ApexLending.Auth`), sliding 8-hour expiration.
- **Roles**:
  - `Applicant` (Can view own applications, ask policy copilot questions, upload documents, confirm fields)
  - `LoanOfficer` (Can view review queue, inspect applications, review specialist agent drafts, make binding decisions)
  - `ComplianceReviewer` (Can inspect regulatory compliance, audit trails, TRID disclosures, and security logs)
  - `Administrator` (Can perform system administration and trigger policy index synchronization)
- **Automatic Seeding**:
  - Deterministically seeded in `SeedIdentityUsersAndRolesAsync` if not present.
  - User accounts:
    - `applicant@apex.local` (Alice Cooper, linked to `APP-2026-001`)
    - `officer@apex.local` (Marcus Vance)
    - `compliance@apex.local` (Sarah Jenkins)
    - `admin@apex.local` (System Administrator)
  - Password format: Seeded with default development test passwords in `SeedIdentityUsersAndRolesAsync` (e.g., `Applicant123!`, `Officer123!`, `Compliance123!`, `Admin123!`).
- **Security Headers & Isolation**:
  - Client headers `X-Actor-Id` and `X-Actor-Role` on `/api/mcp` are strictly gated to `Development` environment or authenticated callers.

---

## 13. Azure OpenAI Requirements

- **Status**: `VERIFIED`
- **Cloud Dependency**: Persistent Azure AI Foundry resource in Azure.
- **Resource Details**:
  - Endpoint: `https://proj-loan-assistant-1-resource.openai.azure.com/` (configured in User Secrets)
  - API Key: `[REDACTED — SECRET EXISTS]` (configured in User Secrets)
  - Chat Deployment: `gpt-4o`
  - Embedding Deployment: `text-embedding-3-small`
- **SDK**: `Azure.AI.OpenAI` v2.1.0 (`AzureOpenAIClient`, `ChatClient`, `EmbeddingClient`).
- **Consumers**:
  - `AzureOpenAIChatModel` (Copilot chat assistant across all personas)
  - `AzureOpenAiDocumentExtractor` (Structured document fact extraction)
  - `RecommendationOrchestratorAgent` (Multi-agent summary reasoning)
  - `PolicyIndexer` (Embedding generation for policy chunks)
  - `AzureAiSearchPolicyRetriever` (Query vector generation)
- **Company Laptop Action**:
  - The Azure resource is in the cloud and already deployed. The company laptop does NOT need a new resource; it only needs the endpoint, deployments, and API key configured in its local User Secrets.

---

## 14. Azure AI Search Requirements

- **Status**: `VERIFIED`
- **Cloud Dependency**: Persistent Azure AI Search resource in Azure.
- **Resource Details**:
  - Endpoint: `https://loan-assistant-search.search.windows.net` (configured in User Secrets)
  - API Key: `[REDACTED — SECRET EXISTS]` (configured in User Secrets)
  - Index Name: `loan-policies-index`
- **Search Capabilities**: Hybrid Vector + Full-Text Search (HNSW Cosine Vector search + semantic keyword match).
- **Index State**: The index already exists in Azure and is populated.
- **Company Laptop Action**:
  - Can immediately query the existing cloud index using the User Secrets credentials.
  - If re-indexing is desired, the tracked markdown files in `src/Loan.Infrastructure/Search/SeedPolicies/*.md` allow one-click synchronization via `/Admin` or `PolicyIndexer`.

---

## 15. Azure Blob Storage Requirements

- **Status**: `VERIFIED`
- **Storage Strategy**: Dual-mode (Azure Blob Storage with automatic Local File Fallback).
- **Interface**: `IDocumentStorageService`
- **Implementation**: `AzureBlobDocumentStorageService`
  - Azure Connection: `AzureStorage:ConnectionString` in User Secrets.
  - Container Name: `loan-documents` (Private access).
  - Fallback: If connection string is missing or invalid, automatically falls back to `LocalFileDocumentStorageService` (`src/Loan.Web/App_Data/Uploads/`).
- **Company Laptop Action**:
  - If corporate laptop permits Azure Storage connection strings, set `AzureStorage:ConnectionString` in User Secrets.
  - If blocked by corporate policy, the application seamlessly runs using local fallback file storage without errors.

---

## 16. Document Extraction Requirements

- **Status**: `VERIFIED`
- **Primary Engine**: `AzureOpenAiDocumentExtractor` using GPT-4o with structured JSON schema output and confidence scoring.
- **Fallback Engine**: `SyntheticDocumentExtractor` (deterministic regex/rule-based parser for offline/test environments).
- **Sample Fixtures**: 6 synthetic files in `src/Loan.Web/SampleDocuments/` (all tracked in Git).
- **Company Laptop Action**:
  - Fully functional out of the box with sample documents.

---

## 17. MCP Requirements

- **Status**: `VERIFIED`
- **Protocol**: Model Context Protocol (MCP) JSON-RPC 2.0 (protocolVersion: `2024-11-05`).
- **Endpoint**: `/api/mcp` (POST) in `src/Loan.Web/Controllers/McpController.cs`.
- **Server Class**: `McpToolServer` in `src/Loan.Infrastructure/MCP/McpToolServer.cs`.
- **Tools Registered**:
  - `get_identity_status`
  - `get_income_status`
  - `get_credit_status`
  - `search_policy`
  - `save_recommendation_draft`
- **Company Laptop Action**:
  - Zero external tool runtime required; MCP is implemented as an in-process and HTTP service within ASP.NET Core.

---

## 18. Agent/Orchestration Requirements

- **Status**: `VERIFIED`
- **Specialist Agents**:
  - `DocumentAnalysisAgent`
  - `EligibilityAnalysisAgent`
  - `ComplianceReviewAgent`
  - `RecommendationOrchestratorAgent`
- **Semantic Kernel Integration**: `Microsoft.SemanticKernel` v1.34.0 registered in DI with native plugins:
  - `IdentityPlugin`, `CreditPlugin`, `PolicySearchPlugin`, `DraftSaverPlugin`.
- **Safety Boundaries**:
  - Deterministic calculations are strictly executed in C# (`Loan.Domain.Eligibility`).
  - Agents can only create drafts (`DraftPreparedBySystem`).
  - Final decisions require Loan Officer human approval via `OfficerDecisionCommandHandler`.
- **Company Laptop Action**:
  - Fully self-contained in application code.

---

## 19. Worker Requirements

- **Status**: `VERIFIED`
- **Project**: `Loan.Workers` (`src/Loan.Workers/Loan.Workers.csproj`).
- **Nature**: Background Worker service (`Host.CreateApplicationBuilder`).
- **Hosted Services**:
  - `PolicyIndexingWorker`: Periodically synchronizes seed policies into Azure AI Search.
  - `DocumentProcessingWorker`: Simulates background document intake queue.
- **Web App Dependency**:
  - The main Web application (`Loan.Web`) is **independent** and does NOT require `Loan.Workers` to be running for standard user workflows, as `Loan.Web` has its own `/Admin` sync trigger and on-demand document extraction.
- **Company Laptop Action**:
  - `Loan.Workers` can be run when background indexing is desired, but is optional for normal portal evaluation.

---

## 20. Local File/Machine Dependencies

- **Status**: `VERIFIED`
- **Absolute Path Check**: **Zero** hardcoded machine paths (`C:\`, `/Users/`, etc.) exist in application C# code or configuration files.
- **Port Bindings**:
  - `http://localhost:5069` (HTTP)
  - `https://localhost:7229` (HTTPS)
  - Configured via relative launch profile in `launchSettings.json`.
- **Storage Directories**:
  - `App_Data/Uploads` uses relative path `Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "Uploads")`.
  - Automatically created on startup if it does not exist.

---

## 21. Security/Secret Scan

- **Status**: `VERIFIED`
- **Scan Method**: Comprehensive programmatic pattern scan across all 255 Git-tracked files.
- **Findings**:
  - **Zero** hardcoded API keys found in tracked files.
  - **Zero** real connection strings with plaintext passwords in tracked files.
  - `appsettings.json` contains only safe dummy placeholders (`YOUR-STORAGE-ACCOUNT`, `YOUR-RESOURCE-NAME`, etc.).
  - All real credentials reside strictly in developer User Secrets.
  - PII in synthetic sample documents is completely synthetic and masked (e.g., `***-**-6789`).

---

## 22. Demo/Synthetic Data Inventory

- **Status**: `VERIFIED`
- **Data Reproducibility**: 100% of required demo data is generated deterministically by code and seed files.

| Entity | Count | Source of Truth | Recreated Automatically? |
| :--- | :--- | :--- | :--- |
| Identity Roles | 4 | `SeedIdentityUsersAndRolesAsync()` | **YES** |
| Identity Users | 4 | `SeedIdentityUsersAndRolesAsync()` | **YES** |
| Policy Documents | 8 | `src/Loan.Infrastructure/Search/SeedPolicies/*.md` | **YES** |
| Sample Applicant Stubs | 6 | `src/Loan.Web/SampleDocuments/*.txt` | **YES** |
| Canonical Demo Loans | 4 | `DemoDataSeeder.SeedDemoApplicationsAsync()` | **YES** |
| Evaluation Benchmark | 20 prompts | `docs/evaluation_results.json` | **YES** |

---

## 23. Required Company-Laptop Software

- **Status**: `REQUIRES USER ACTION`
- The company laptop must have:

1. **Operating System**: Windows 10/11 (or macOS/Linux, though project was developed on Windows).
2. **.NET SDK**: **.NET 8.0 SDK** (minimum 8.0.400+) or **.NET 10.0 SDK**.
   - Download: `https://dotnet.microsoft.com/download/dotnet/8.0`
3. **Database Engine**:
   - **SQL Server LocalDB** (`(localdb)\MSSQLLocalDB`)
   - *Alternative if LocalDB is restricted*: SQL Server 2022 Developer Edition, or SQL Server Express, or Docker container `mcr.microsoft.com/mssql/server:2022-latest`.
4. **Git Client**: Git 2.40+ (`https://git-scm.com/`).
5. **IDE / Editor**: Visual Studio 2022 (v17.8+ with ASP.NET and Web Development workload), VS Code (with C# Dev Kit extension), or JetBrains Rider.
6. **EF Core Global Tool** (Optional but recommended):
   ```bash
   dotnet tool install --global dotnet-ef
   ```

---

## 24. Required Azure Access

- **Status**: `VERIFIED`
- The company laptop must be able to reach:
  1. `https://proj-loan-assistant-1-resource.openai.azure.com/` on port 443.
  2. `https://loan-assistant-search.search.windows.net` on port 443.
  3. `https://loanassistantstorage01.blob.core.windows.net` on port 443.
- **Corporate Consideration**: If the company laptop is behind an SSL-intercepting corporate proxy (e.g., Zscaler, BlueCoat), the root CA certificate must be trusted by Windows certificate store for .NET HTTP requests to succeed.

---

## 25. Critical Migration Blockers

- **Status**: `VERIFIED`

### Blocker 1: User Secrets Must Be Configured on Target Machine [SEVERITY: BLOCKER]
- **Detail**: Git does not track User Secrets. If `Loan.Web` is started without secrets, `AzureOpenAI` and `AzureAISearch` will be missing, and the `/health/details` endpoint will report degraded services.
- **Resolution**: Run `dotnet user-secrets set` commands on the company laptop before launching the web app.

### Blocker 2: SQL Server LocalDB Instance Must Be Available [SEVERITY: BLOCKER]
- **Detail**: The default connection string targets `(localdb)\MSSQLLocalDB`. If the company laptop does not have LocalDB installed, startup migration will throw a `SqlException` ("A network-related or instance-specific error occurred...").
- **Resolution**: Ensure LocalDB is installed or point `ConnectionStrings:DefaultConnection` to a local SQL Server Express or Docker instance.

---

## 26. Migration Risks

- **Status**: `VERIFIED`

| Risk | Severity | Impact | Mitigation Strategy |
| :--- | :--- | :--- | :--- |
| Corporate Proxy blocking Azure AI endpoints | **HIGH** | Live Copilot & RAG fail | Verify endpoint connectivity via PowerShell `curl` or browser before running |
| LocalDB not permitted by corporate security policy | **HIGH** | Cannot connect to database | Configure Docker SQL Server or developer edition and update connection string in User Secrets |
| Missing .NET 8 runtime on company laptop | **MEDIUM** | Build fails | Install .NET 8 SDK or .NET 10 SDK before cloning |
| Local upload directory permissions | **LOW** | Document uploads fail | Application runs in user space; standard write permissions to `App_Data` apply |
| Port 5069 already in use on target laptop | **LOW** | Port conflict | Change `applicationUrl` in `Properties/launchSettings.json` or run `dotnet run --urls "http://localhost:5070"` |

---

## 27. Recommended Migration Order

> [!IMPORTANT]
> **Follow this exact sequence on the new company laptop:**

1. **Step 1: Verify Prerequisites on Company Laptop**:
   - Check .NET SDK: `dotnet --version` (Ensure .NET 8.0+ is installed).
   - Check Git: `git --version`.
   - Check SQL Server LocalDB: `sqllocaldb info` (Ensure `MSSQLLocalDB` exists).
2. **Step 2: Clone the Repository**:
   ```bash
   git clone https://github.com/AayushMathur03/LoanAssistant.git
   cd LoanAssistant
   ```
3. **Step 3: Restore & Build Solution**:
   ```bash
   dotnet restore LoanAssistant.slnx
   dotnet build LoanAssistant.slnx
   ```
4. **Step 4: Run Baseline Automated Tests**:
   ```bash
   dotnet test LoanAssistant.slnx --no-build
   ```
   *(Verify all 161 tests pass 100% green).*
5. **Step 5: Configure User Secrets for `Loan.Web`**:
   ```bash
   cd src/Loan.Web
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=LoanAssistantDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
   dotnet user-secrets set "AzureOpenAI:Endpoint" "https://proj-loan-assistant-1-resource.openai.azure.com/"
   dotnet user-secrets set "AzureOpenAI:ApiKey" "<YOUR_AZURE_OPENAI_API_KEY>"
   dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4o"
   dotnet user-secrets set "AzureOpenAI:EmbeddingDeploymentName" "text-embedding-3-small"
   dotnet user-secrets set "AzureAISearch:Endpoint" "https://loan-assistant-search.search.windows.net"
   dotnet user-secrets set "AzureAISearch:ApiKey" "<YOUR_AZURE_AI_SEARCH_API_KEY>"
   dotnet user-secrets set "AzureAISearch:IndexName" "loan-policies-index"
   dotnet user-secrets set "AzureStorage:ConnectionString" "<YOUR_AZURE_STORAGE_CONNECTION_STRING>"
   dotnet user-secrets set "AzureStorage:ContainerName" "loan-documents"
   ```
6. **Step 6: Launch Web Application**:
   ```bash
   dotnet run
   ```
   *(The application will automatically create `LoanAssistantDb`, run all 3 migrations, seed Identity roles and users, and seed the 4 canonical applications).*
7. **Step 7: Verify Application Health**:
   - Open browser at `http://localhost:5069/health/details`.
   - Verify `database`, `azureOpenAi`, and `azureAiSearch` report `Healthy`.
8. **Step 8: Verify Portal Workflows**:
   - Log in as Applicant (`applicant@apex.local` / `Applicant123!`).
   - Log in as Loan Officer (`officer@apex.local` / `Officer123!`).
   - Log in as Compliance Reviewer (`compliance@apex.local` / `Compliance123!`).
   - Log in as Administrator (`admin@apex.local` / `Admin123!`).

---

## 28. Rollback Plan

- **Personal Laptop Preservation**:
  - The personal laptop remains completely untouched, operational, and with its working database and secrets preserved.
- **Cloud Resources Safety**:
  - The migration procedure does NOT alter Azure OpenAI models, delete Azure AI Search indexes, or drop Azure Storage containers.
- **Abandonment Procedure**:
  - If company laptop setup fails (e.g. strict corporate proxy blocks or software policy issues), simply delete the cloned directory on the company laptop. The personal development environment continues to run with zero impact.

---

## 29. Pre-Migration Checklist

- [ ] All code changes committed and pushed to `origin/main` (`e39353f`).
- [ ] Automated test suite passes 100% (161/161 tests green).
- [ ] Azure OpenAI resource is active in Azure portal.
- [ ] Azure AI Search resource is active in Azure portal.
- [ ] Azure Storage account is active in Azure portal.
- [ ] Target company laptop has .NET 8.0+ SDK installed.
- [ ] Target company laptop has SQL Server LocalDB or reachable SQL instance.
- [ ] User secrets values documented securely for manual entry.

---

## 30. Information You Must Provide Before Migration

You will need the following 5 values available to configure User Secrets on the company laptop:

1. **Azure OpenAI API Key**: The primary or secondary key for `https://proj-loan-assistant-1-resource.openai.azure.com/`.
2. **Azure AI Search API Key**: The admin or query key for `https://loan-assistant-search.search.windows.net`.
3. **Azure Blob Storage Connection String**: The connection string for storage account `loanassistantstorage01` (or omit to use local disk fallback).
4. **Target SQL Server Connection String**: If your company laptop cannot use `(localdb)\MSSQLLocalDB`, the connection string to your company laptop's SQL instance.
5. **GitHub Personal Access Token / SSH Key**: Authorized to clone `https://github.com/AayushMathur03/LoanAssistant.git` on the company laptop.
