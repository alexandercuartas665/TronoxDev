namespace Tronox.Application.Terceros;

/// <summary>
/// Catalogo de Terceros (RQ07 RF01): directorio maestro unico de actores externos (DAT-02). CRUD sin
/// eliminacion (solo inactivacion con motivo), dedup por (tenant, tipo_documento, numero_documento) y
/// creacion rapida desde otros modulos. Tenant-scoped por el filtro global. Resultado tipado.
/// </summary>
public interface ITerceroService
{
    Task<TerceroListResult> ListarAsync(TerceroFiltro filtro, CancellationToken ct = default);
    Task<TerceroFichaDto?> ObtenerAsync(long id, CancellationToken ct = default);

    /// <summary>Crea (Id=0) o actualiza un tercero. Valida obligatorios por subtipo y unicidad de documento.</summary>
    Task<TerceroResult> GuardarAsync(TerceroFichaDto ficha, CancellationToken ct = default);

    /// <summary>Inactiva un tercero con motivo obligatorio (RF01-5). No elimina: conserva los vinculos.</summary>
    Task<TerceroResult> InactivarAsync(long id, string motivo, CancellationToken ct = default);
    Task<TerceroResult> ReactivarAsync(long id, string motivo, CancellationToken ct = default);

    /// <summary>Busca un tercero por tipo+numero de documento (dedup/autocompletado). Null si no existe.</summary>
    Task<TerceroSugerenciaDto?> BuscarPorDocumentoAsync(string tipoDocumento, string numeroDocumento, CancellationToken ct = default);

    /// <summary>Crea o actualiza (upsert por documento) un tercero con datos minimos desde otro modulo
    /// (RF01-4): nace con perfil_completo=false ("Incompleto"). Devuelve el Id del tercero.</summary>
    Task<TerceroResult> CrearRapidoAsync(CrearRapidoRequest request, CancellationToken ct = default);
}
