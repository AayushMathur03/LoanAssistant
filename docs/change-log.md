# Change Log

All notable changes to the Loan Application & Compliance Review Assistant project will be documented in this file.

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
