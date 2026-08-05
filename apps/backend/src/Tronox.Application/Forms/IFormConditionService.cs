namespace Tronox.Application.Forms;

/// <summary>
/// CRUD de reglas condicionales autocontenidas de una definicion de formulario (RQ08), consumido por
/// la pestana "Reglas" del diseñador. Las filas son tenant-scoped por el filtro global; la evaluacion
/// en runtime la hace <see cref="FormConditionEvaluator"/> (puro). No hay motor de Reglas externo.
/// </summary>
public interface IFormConditionService
{
    /// <summary>Reglas de la definicion ordenadas por SortOrder.</summary>
    Task<IReadOnlyList<FormFieldConditionDto>> ListAsync(long definitionId, CancellationToken cancellationToken = default);

    /// <summary>Agrega una regla validando operador y accion; le asigna el siguiente SortOrder.</summary>
    Task<FormResult<FormFieldConditionDto>> AddAsync(long definitionId, SaveFormFieldConditionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Elimina una regla (tenant-scoped).</summary>
    Task<FormResult<bool>> DeleteAsync(long conditionId, CancellationToken cancellationToken = default);
}
