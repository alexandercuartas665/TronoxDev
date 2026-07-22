using Tronox.Domain.Enums;

namespace Tronox.Application.Admin;

public interface IPaymentAdminService
{
    /// <summary>Devuelve null si la suscripcion no existe o no pertenece al tenant indicado.</summary>
    Task<PaymentDetail?> RegisterAsync(RegisterPaymentRequest request, long actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentDetail>> ListAsync(long? tenantId = null, PaymentStatus? status = null, CancellationToken cancellationToken = default);
}
