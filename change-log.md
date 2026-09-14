# Change Log

All notable changes to the Loan Application & Compliance Review Assistant project will be documented in this file.

## [Unreleased] - 2026-09-14

### Added
- Created project tracking documentation: `docs/implementation-status.md`, `task-checklist.md`, `change-log.md`, `decision-log.md`.
- Implemented core Domain Layer in `Loan.Domain` (11 unit tests passing).
- Implemented Application Layer in `Loan.Application` (5 unit tests passing).
- Implemented Infrastructure Layer in `Loan.Infrastructure` (5 contract & integration tests passing).
- Implemented Presentation & Modern Fintech UI Layer in `Loan.Web` (4 E2E tests passing).
- Implemented Workers Layer in `Loan.Workers`:
  - `DocumentProcessingWorker`: Hosted background service polling document intake queue (**FR-02**).
  - `PolicyIndexingWorker`: Hosted background service managing versioned policy index sync.
- Implemented Prompt Evaluation Suite in `tests/Loan.PromptTests/`:
  - `PromptEvaluationDataset`: Repeatable dataset covering Grounded RAG, missing evidence fallbacks, and adversarial prompt injection refusal (**Flow D**).
  - `PromptEvaluationTests`: NUnit test runner evaluating prompt responses for groundedness, policy citations (**BR-04**), disclaimers, and refusal of illegal prompt overrides (**BR-01**, **BR-07** - 4 tests passing).
- Achieved **29/29 tests passing** (100% pass rate across all 6 test projects in `LoanAssistant.slnx`).
