namespace Loan.Application.DTOs;

public record CitationDto(
    string DocumentTitle,
    string PolicyVersion,
    string SectionOrPage,
    string Excerpt);

public record PolicySearchResultDto(
    string DocumentId,
    string Title,
    string Version,
    string Section,
    string Content,
    double SimilarityScore,
    CitationDto Citation);
