---
DocumentId: DOC-EXCEPTIONS-V1
Title: Compliance Exceptions and Manual Escalation Policy v1.0
ProductId: ALL
PolicyVersion: v1.0
DocumentType: CompliancePolicy
Audience: LoanOfficer
EffectiveFrom: 2026-01-01
EffectiveTo: Active
---

# Compliance Exceptions and Manual Escalation Policy v1.0

## Section 1.0 Purpose
### 1.1 Objective
This synthetic policy defines controlled exceptions and manual escalation paths.

## Section 2.0 DTI Exceptions
### 2.1 Standard Threshold
Product DTI limits remain authoritative unless a documented human exception is approved.
### 2.2 Exception Ceiling
An officer may consider a DTI exception up to 45% when documented compensating factors are present.
### 2.3 Compensating Factors
Example factor: high liquid reserves exceeding 12 months of PITI.

## Section 3.0 Credit Score Exceptions
### 3.1 Manual Review
Credit-score conditions outside standard policy require documented manual review.
### 3.2 No Automatic Override
The AI assistant cannot override deterministic credit rules or directly change application status.

## Section 4.0 Secondary Income Documentation
### 4.1 Additional Evidence
Secondary or non-standard income may require additional documentation.
### 4.2 Completeness
Missing required evidence keeps the recommendation `Pending Information`.

## Section 5.0 Officer Justification
### 5.1 Written Note
Every approved exception requires a written officer justification note covering exception type, policy condition, evidence/compensating factor, rationale, and officer identity.
### 5.2 Audit Logging
Every consequential exception action must create an audit entry with timestamp and correlation ID.

## Section 6.0 Escalation
### 6.1 Manual Review
Out-of-policy conditions, conflicting evidence, unresolved compliance issues, or unavailable verification factors are routed for human/manual review.
### 6.2 No Model Decision
The assistant must not turn an out-of-policy condition into a model-made approval or rejection.
