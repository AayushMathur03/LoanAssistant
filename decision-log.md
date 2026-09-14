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

## ADR-005: Azure AI Search & Embedding Model Hybrid RAG Implementation
- **Date**: 2026-09-14
- **Status**: Accepted
- **Context**: Slice 3 requires versioned policy documents (markdown format with YAML frontmatter) and a hybrid vector search policy retriever for grounding assistant responses.
- **Decision**: Installed `Azure.Search.Documents` v11.6.0. Generated 8 synthetic versioned policy documents in `src/Loan.Infrastructure/Search/SeedPolicies/`. Created `PolicyIndexer` (automating HNSW vector index creation and document chunking/embedding via Azure OpenAI `text-embedding-3-small` with 1536 dimensions) and `AzureAiSearchPolicyRetriever` (implementing hybrid keyword + vector retrieval with metadata filtering on `productId` and `policyVersion`). Maintained graceful fallback to `SyntheticPolicyRetriever` when cloud credentials are missing.
- **Consequences**: Full policy document RAG pipeline with section-level citations and version awareness connected to Azure AI Search.
