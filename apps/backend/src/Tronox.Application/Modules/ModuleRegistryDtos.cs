using Tronox.Domain.Enums;

namespace Tronox.Application.Modules;

/// <summary>
/// Fila del catalogo global con el estado del tenant activo (module registry, legacy 000109).
/// IsEnabled refleja el TenantModule del tenant; si no existe fila, el modulo esta
/// deshabilitado por defecto (opt-in explicito por tenant).
/// </summary>
public sealed record ModuleCatalogRowDto(
    long ModuleDefinitionId,
    string LegacyCode,
    string Name,
    string? Description,
    string? Route,
    ModuleArea Area,
    bool IsCore,
    bool IsEnabled,
    string? SettingsJson);

/// <summary>Modulo habilitado de un tenant (para derivar el menu del registry).</summary>
public sealed record EnabledModuleDto(
    long ModuleDefinitionId,
    string LegacyCode,
    string Name,
    string? Route,
    ModuleArea Area,
    bool IsCore,
    string? SettingsJson);

/// <summary>Alta/edicion de una definicion del catalogo GLOBAL (solo PlatformAdmin).</summary>
public sealed record SaveModuleDefinitionRequest(
    string LegacyCode,
    string Name,
    string? Description = null,
    string? Route = null,
    ModuleArea Area = ModuleArea.Principal,
    bool IsCore = false);
