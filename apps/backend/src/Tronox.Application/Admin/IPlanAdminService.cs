namespace Tronox.Application.Admin;

public interface IPlanAdminService
{
    Task<PlanDetail> CreateAsync(CreatePlanRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<PlanDetail?> UpdateAsync(long id, CreatePlanRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlanDetail>> ListAsync(CancellationToken cancellationToken = default);
    Task<PlanDetail?> GetAsync(long id, CancellationToken cancellationToken = default);
    Task<PlanDetail?> SetActiveAsync(long id, bool isActive, long actorUserId, CancellationToken cancellationToken = default);
}
