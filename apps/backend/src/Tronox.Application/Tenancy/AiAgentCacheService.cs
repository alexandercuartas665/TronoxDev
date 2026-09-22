using System.Globalization;
using System.Text;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Tronox.Application.Tenancy;

/// <summary>Datos cache de un agente (RQ16, port de ECOREX). Ver IAiAgentCacheService.</summary>
public sealed class AiAgentCacheService : IAiAgentCacheService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public AiAgentCacheService(IApplicationDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<IReadOnlyList<AiAgentCacheFieldDto>> ListFieldsAsync(long agentId, CancellationToken cancellationToken = default)
    {
        return await _db.AiAgentCacheFields.AsNoTracking()
            .Where(f => f.AgentId == agentId)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Label)
            .Select(f => new AiAgentCacheFieldDto(f.Id, f.AgentId, f.FieldKey, f.Label, f.Description, f.SortOrder, f.IsUpdatable))
            .ToListAsync(cancellationToken);
    }

    public async Task<AiAgentCacheFieldDto?> CreateFieldAsync(CreateAgentCacheFieldRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is not long tenantId) { return null; }
        var agent = await _db.AiAgents.FirstOrDefaultAsync(a => a.Id == request.AgentId, cancellationToken);
        if (agent is null) { return null; }

        var label = (request.Label ?? "Dato").Trim();
        if (label.Length == 0) { return null; }

        var existing = await _db.AiAgentCacheFields.Where(f => f.AgentId == request.AgentId)
            .Select(f => f.FieldKey).ToListAsync(cancellationToken);
        var key = EnsureUniqueKey(Slugify(label), existing);

        var nextOrder = (await _db.AiAgentCacheFields.Where(f => f.AgentId == request.AgentId)
            .Select(f => (int?)f.SortOrder).MaxAsync(cancellationToken) ?? -1) + 1;

        var field = new AiAgentCacheField
        {
            TenantId = tenantId,
            AgentId = request.AgentId,
            FieldKey = key,
            Label = label,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            SortOrder = nextOrder,
            IsUpdatable = request.IsUpdatable
        };
        _db.AiAgentCacheFields.Add(field);
        _audit.Write(actorUserId, "ai-agent.cache-field.create", nameof(AiAgentCacheField), field,
            previousValue: null, newValue: new { request.AgentId, field.FieldKey, field.Label }, tenantId: tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return new AiAgentCacheFieldDto(field.Id, field.AgentId, field.FieldKey, field.Label, field.Description, field.SortOrder, field.IsUpdatable);
    }

    public async Task<AiAgentCacheFieldDto?> UpdateFieldAsync(long fieldId, UpdateAgentCacheFieldRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var field = await _db.AiAgentCacheFields.FirstOrDefaultAsync(f => f.Id == fieldId, cancellationToken);
        if (field is null) { return null; }
        var label = (request.Label ?? field.Label).Trim();
        if (label.Length == 0) { return null; }
        field.Label = label;
        field.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        field.IsUpdatable = request.IsUpdatable;
        await _db.SaveChangesAsync(cancellationToken);
        return new AiAgentCacheFieldDto(field.Id, field.AgentId, field.FieldKey, field.Label, field.Description, field.SortOrder, field.IsUpdatable);
    }

    public async Task<bool> DeleteFieldAsync(long fieldId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var field = await _db.AiAgentCacheFields.FirstOrDefaultAsync(f => f.Id == fieldId, cancellationToken);
        if (field is null) { return false; }
        var orphans = _db.AiAgentCacheValues.Where(v => v.AgentId == field.AgentId && v.FieldKey == field.FieldKey);
        _db.AiAgentCacheValues.RemoveRange(orphans);
        _db.AiAgentCacheFields.Remove(field);
        _audit.Write(actorUserId, "ai-agent.cache-field.delete", nameof(AiAgentCacheField), field,
            previousValue: new { field.FieldKey, field.Label }, newValue: null, tenantId: field.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<AiAgentCacheValueDto>> GetValuesAsync(long agentId, long sessionId, CancellationToken cancellationToken = default)
    {
        var fields = await _db.AiAgentCacheFields.AsNoTracking()
            .Where(f => f.AgentId == agentId)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Label)
            .ToListAsync(cancellationToken);
        if (fields.Count == 0) { return Array.Empty<AiAgentCacheValueDto>(); }

        var values = await _db.AiAgentCacheValues.AsNoTracking()
            .Where(v => v.AgentId == agentId && v.SessionId == sessionId)
            .ToDictionaryAsync(v => v.FieldKey, cancellationToken);

        return fields.Select(f =>
        {
            values.TryGetValue(f.FieldKey, out var v);
            return new AiAgentCacheValueDto(f.FieldKey, f.Label, f.Description, v?.Value, v?.Source, v?.UpdatedAt);
        }).ToList();
    }

    public async Task<AiAgentCacheValueDto?> SetValueAsync(SetAgentCacheValueRequest request, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is not long tenantId) { return null; }
        var field = await _db.AiAgentCacheFields.AsNoTracking()
            .FirstOrDefaultAsync(f => f.AgentId == request.AgentId && f.FieldKey == request.FieldKey, cancellationToken);
        if (field is null) { return null; }

        var entry = await _db.AiAgentCacheValues.FirstOrDefaultAsync(
            v => v.AgentId == request.AgentId && v.SessionId == request.SessionId && v.FieldKey == request.FieldKey,
            cancellationToken);
        if (entry is null)
        {
            entry = new AiAgentCacheValue
            {
                TenantId = tenantId,
                AgentId = request.AgentId,
                SessionId = request.SessionId,
                FieldKey = request.FieldKey,
                Value = request.Value,
                Source = request.Source
            };
            _db.AiAgentCacheValues.Add(entry);
        }
        else
        {
            // Si el campo es sticky (no actualizable) y ya tenia valor, no lo sobrescribimos.
            if (!field.IsUpdatable && !string.IsNullOrWhiteSpace(entry.Value))
            {
                return new AiAgentCacheValueDto(field.FieldKey, field.Label, field.Description, entry.Value, entry.Source, entry.UpdatedAt);
            }
            entry.Value = request.Value;
            entry.Source = request.Source;
        }
        await _db.SaveChangesAsync(cancellationToken);
        return new AiAgentCacheValueDto(field.FieldKey, field.Label, field.Description, entry.Value, entry.Source, entry.UpdatedAt);
    }

    public async Task<int> ClearValuesAsync(long agentId, long sessionId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var values = await _db.AiAgentCacheValues
            .Where(v => v.AgentId == agentId && v.SessionId == sessionId)
            .ToListAsync(cancellationToken);
        if (values.Count == 0) { return 0; }
        _db.AiAgentCacheValues.RemoveRange(values);
        _audit.Write(actorUserId, "ai-agent.cache.clear", nameof(AiAgentCacheValue), agentId,
            previousValue: new { count = values.Count, sessionId }, newValue: null, tenantId: _tenantContext.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return values.Count;
    }

    // --- helpers ---
    private static string Slugify(string label)
    {
        var normalized = label.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
        }
        var slug = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(slug) ? "dato" : slug;
    }

    private static string EnsureUniqueKey(string baseKey, IReadOnlyCollection<string> existingKeys)
    {
        if (!existingKeys.Contains(baseKey)) { return baseKey; }
        var i = 2;
        while (existingKeys.Contains($"{baseKey}_{i}")) { i++; }
        return $"{baseKey}_{i}";
    }
}
