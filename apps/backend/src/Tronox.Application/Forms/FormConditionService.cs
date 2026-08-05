using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;

namespace Tronox.Application.Forms;

/// <summary>
/// Implementacion EF de <see cref="IFormConditionService"/> (RQ08): CRUD de reglas condicionales
/// autocontenidas por definicion. Tenant-scoped por el filtro global; la evaluacion en runtime la
/// hace <see cref="FormConditionEvaluator"/> (puro). No hay motor de Reglas externo.
/// </summary>
public sealed class FormConditionService : IFormConditionService
{
    private static readonly HashSet<string> Operators =
        new(["equals", "notEquals", "empty", "notEmpty", "contains", "gt", "lt"], StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Actions =
        new(["show", "hide", "require", "optional", "setValue"], StringComparer.OrdinalIgnoreCase);

    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    public FormConditionService(IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<FormFieldConditionDto>> ListAsync(long definitionId, CancellationToken cancellationToken = default)
        => await _db.FormFieldConditions.AsNoTracking()
            .Where(c => c.DefinitionId == definitionId)
            .OrderBy(c => c.SortOrder)
            .Select(c => new FormFieldConditionDto(
                c.Id, c.SourceFieldCode, c.Operator, c.Value, c.Action, c.TargetFieldCode, c.SetValue, c.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<FormResult<FormFieldConditionDto>> AddAsync(
        long definitionId, SaveFormFieldConditionRequest request, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is not long tenantId)
        {
            return FormResult<FormFieldConditionDto>.Invalid("No hay tenant activo.");
        }
        if (!await _db.FormDefinitions.AnyAsync(d => d.Id == definitionId, cancellationToken))
        {
            return FormResult<FormFieldConditionDto>.NotFound("El formulario no existe.");
        }
        if (string.IsNullOrWhiteSpace(request.SourceFieldCode) || string.IsNullOrWhiteSpace(request.TargetFieldCode))
        {
            return FormResult<FormFieldConditionDto>.Invalid("Los campos origen y destino son obligatorios.");
        }
        if (!Operators.Contains(request.Operator))
        {
            return FormResult<FormFieldConditionDto>.Invalid("Operador no soportado.");
        }
        if (!Actions.Contains(request.Action))
        {
            return FormResult<FormFieldConditionDto>.Invalid("Accion no soportada.");
        }

        var maxOrder = await _db.FormFieldConditions
            .Where(c => c.DefinitionId == definitionId)
            .MaxAsync(c => (int?)c.SortOrder, cancellationToken) ?? 0;

        var condition = new FormFieldCondition
        {
            TenantId = tenantId,
            DefinitionId = definitionId,
            SourceFieldCode = request.SourceFieldCode.Trim(),
            Operator = request.Operator,
            Value = request.Value,
            Action = request.Action,
            TargetFieldCode = request.TargetFieldCode.Trim(),
            SetValue = request.SetValue,
            SortOrder = maxOrder + 1
        };
        _db.FormFieldConditions.Add(condition);
        await _db.SaveChangesAsync(cancellationToken);

        return FormResult<FormFieldConditionDto>.Ok(new FormFieldConditionDto(
            condition.Id, condition.SourceFieldCode, condition.Operator, condition.Value,
            condition.Action, condition.TargetFieldCode, condition.SetValue, condition.SortOrder));
    }

    public async Task<FormResult<bool>> DeleteAsync(long conditionId, CancellationToken cancellationToken = default)
    {
        var condition = await _db.FormFieldConditions.FirstOrDefaultAsync(c => c.Id == conditionId, cancellationToken);
        if (condition is null)
        {
            return FormResult<bool>.NotFound("La regla no existe.");
        }
        _db.FormFieldConditions.Remove(condition);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }
}
