# Change Log

All notable changes to the Loan Application & Compliance Review Assistant project will be documented in this file.

## [Slice 1 Release Gate Passed] - 2026-09-14

### Added / Verified
- **EF Core Migrations & SQL Server Persistence**:
  - Reproducible EF Core Migration `20260914103301_InitialCreate.cs` generated and applied to local SQL Server.
  - Startup migration configured to be development-only (`Database:AutoMigrateOnStartup`).
  - Verified physical persistence in SQL Server (`LoanAssistantDb`) via `sqlcmd` and SSMS inspection (`LoanApplications` & `Recommendations` tables populated with seeded data).
- **Secrets Security & Configuration Isolation**:
  - `appsettings.json` updated with safe template connection string without credentials (`Server=YOUR_SERVER;Database=LoanAssistantDb...`).
  - User Secrets store configured for local development (`dotnet user-secrets set ConnectionStrings:DefaultConnection ...`).
- **Clean Architecture Boundary Verification**:
  - Verified `Loan.Domain` has zero dependencies.
  - Verified `Loan.Application` references `Loan.Domain` only.
  - Encapsulated EF Core DI setup in `src/Loan.Infrastructure/DependencyInjection.cs` (`AddInfrastructurePersistence`).
  - `Loan.Web` references `Microsoft.EntityFrameworkCore.Design` strictly for `dotnet ef` CLI tool host compatibility.
- **Testing Suite**:
  - Restored granular repository unit CRUD tests and physical SQL Server disk persistence integration test in `SqlPersistenceIntegrationTests.cs`.
  - **32/32 tests 100% passing** across 6 test projects.
