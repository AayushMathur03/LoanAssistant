# Task Checklist

## Phase 1: Project Setup & Domain Layer (Clean Architecture)
- [x] Inspect existing workspace solution and verify build status (`dotnet build`).
- [x] Create project tracking documentation (`docs/implementation-status.md`, `task-checklist.md`, `change-log.md`, `decision-log.md`).
- [x] Implement Value Objects in `Loan.Domain` (`Money.cs`).
- [x] Implement Aggregate Root & Entities (`LoanApplication.cs`, `ApplicantFacts.cs`, `ProductRules.cs`, `Recommendation.cs`).
- [x] Implement Value Objects for Eligibility & Provenance (`EligibilityIndicators.cs`, `ExtractedField.cs`).
- [x] Implement Domain Exception types (`DomainException`, `InvalidApplicationStateException`, `DomainRuleViolationException`).
- [x] Implement Deterministic Business Rule Calculators (`EligibilityCalculator.cs` for DTI, LTV, Credit Score evaluation).
- [x] Write unit tests in `Loan.Domain.Tests` covering all deterministic rules, boundary cases, and state transitions (11 tests passing).

## Phase 2: Application Layer (Use Cases, CQRS, Interfaces)
- [x] Define interfaces / abstractions (`IChatModel`, `IPolicyRetriever`, `IDocumentExtractor`, `IIdentityReader`, `ICreditReader`, `IIncomeReader`, `ILoanApplicationRepository`, `IRecommendationRepository`).
- [x] Create DTO models (`LoanApplicationDto`, `ApplicantFactsDto`, `EligibilityIndicatorsDto`, `RecommendationDto`, `PolicySearchResultDto`, `CitationDto`, `ExtractedFieldDto`).
- [x] Implement Product Advice (RAG) query handlers (`AskProductQuestionQueryHandler.cs`).
- [x] Implement Verification & Eligibility evaluation orchestration handler (`EvaluateEligibilityCommandHandler.cs`).
- [x] Implement Recommendation & Officer Approval command handlers (`GenerateRecommendationDraftCommandHandler.cs`, `OfficerDecisionCommandHandler.cs`).
- [x] Write unit tests in `Loan.Application.Tests` (5 tests passing).

## Phase 3: Infrastructure Layer (Persistence, AI, Tools, MCP)
- [x] Implement In-Memory Repositories (`InMemoryLoanApplicationRepository`, `InMemoryRecommendationRepository`).
- [x] Implement synthetic verification adapters (`SyntheticIdentityService`, `SyntheticIncomeService`, `SyntheticCreditService`).
- [x] Implement Document Extraction adapter (`SyntheticDocumentExtractor`).
- [x] Implement RAG Policy Search Retriever (`SyntheticPolicyRetriever`).
- [x] Implement Chat Model Adapter (`SyntheticChatModel`).
- [x] Implement MCP Server endpoint handler for approved tools (`get_identity_status`, `get_credit`, `search_policy`).
- [x] Write contract and integration tests (`Loan.ContractTests`, `Loan.IntegrationTests` - 5 tests passing).

## Phase 4: Modern Presentation UI Layer (`Loan.Web`)
- [x] Design modern CSS design system (`fintech-theme.css` with CSS custom properties, cards, status badges, confidence meters, citations panel).
- [x] Implement Navigation & Shell Layout (`_Layout.cshtml` top navbar with persona role switcher).
- [x] Implement Applicant Dashboard & Q&A RAG Chat UI (`ApplicantController`, `Applicant/Index.cshtml` - Flow A & Flow B).
- [x] Implement Officer Workqueue & Side-by-Side Review Screen (`OfficerController`, `Officer/Index.cshtml`, `Officer/Review.cshtml` - Flow C).
- [x] Implement Compliance Reviewer Dashboard (`ComplianceController`, `Compliance/Index.cshtml`).
- [x] Implement Admin Dashboard (`AdminController`, `Admin/Index.cshtml`).
- [x] Wire DI composition root and seed initial demo data in `Program.cs`.
- [x] Write UI End-to-End tests in `Loan.EndToEndTests` (4 tests passing).

## Phase 5: Workers & Evaluation (`Loan.Workers` & `Loan.PromptTests`)
- [x] Implement background document processing worker (`DocumentProcessingWorker.cs`).
- [x] Implement policy index ingestion worker (`PolicyIndexingWorker.cs`).
- [x] Implement prompt evaluation dataset & prompt test runner (`PromptEvaluationDataset.cs`, `PromptEvaluationTests.cs` - 4 tests passing).
- [x] Final end-to-end audit, test verification, and documentation update (29 total tests passing).
