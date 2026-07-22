using Tronox.Domain.Enums;

namespace Tronox.Application.Notifications;

/// <summary>Notificacion in-app para la campana / bandeja del workspace (Ola 7).</summary>
public sealed record NotificationDto(
    long Id,
    NotificationKind Kind,
    string Title,
    string Body,
    string? LinkRoute,
    long? RelatedTaskItemId,
    string? ActorName,
    bool IsRead,
    DateTimeOffset CreatedAt);
