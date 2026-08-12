using EdSpec.Domain.Specifications;

namespace EdSpec.Application.Specifications;

public interface ISpecificationDraftRepository
{
    Task<IReadOnlyCollection<SpecificationDraft>> GetAllAsync(CancellationToken cancellationToken);

    Task<SpecificationDraft?> GetAsync(string id, string version, CancellationToken cancellationToken);

    Task<SpecificationDraft> CreateAsync(SpecificationDraft draft, CancellationToken cancellationToken);

    Task<SpecificationDraft> UpdateAsync(SpecificationDraft draft, CancellationToken cancellationToken);
}
