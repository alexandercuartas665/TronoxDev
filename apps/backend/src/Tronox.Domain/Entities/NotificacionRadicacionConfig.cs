using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Configuracion de una notificacion del modulo de Radicacion por evento (RQ09 RF01-5). Los
/// destinatarios son usuarios especificos o roles de RQ01 (JSON de ids). Algunos eventos son
/// obligatorios por ley (prorroga, incompetencia): siempre activos, no editables en Activo.
/// Unico por (tenant, evento). TENANT-SCOPED.
/// </summary>
public class NotificacionRadicacionConfig : TenantEntity
{
    public RadicacionEventoNotificacion Evento { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>Ids de roles destinatarios (arreglo JSON).</summary>
    public string? DestinatariosRolesJson { get; set; }

    /// <summary>Ids de usuarios destinatarios (arreglo JSON).</summary>
    public string? DestinatariosUsuariosJson { get; set; }

    /// <summary>Asunto de la plantilla (admite variables {{numero_radicado}}, etc.).</summary>
    public string? PlantillaAsunto { get; set; }

    /// <summary>Cuerpo de la plantilla (admite variables).</summary>
    public string? PlantillaCuerpo { get; set; }
}
