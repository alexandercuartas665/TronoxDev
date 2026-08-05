using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Forms;

/// <summary>
/// CRUD de definiciones de formulario dinamico (RQ08, port ECOREX / ADR-0015). El aislamiento por
/// tenant lo da el filtro global. Width (1..12) es la fuente del layout del constructor; GridCol se
/// mantiene sincronizado. Los cambios estructurales sobre una definicion Active incrementan Revision.
/// </summary>
public sealed class FormDefinitionService : IFormDefinitionService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public FormDefinitionService(IApplicationDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    // ---- Definicion ----

    public async Task<IReadOnlyList<FormDefinitionListItemDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = _db.FormDefinitions.AsNoTracking().Where(d => includeArchived || !d.IsArchived);
        var defs = await query.OrderByDescending(d => d.UpdatedAt ?? d.CreatedAt).ToListAsync(cancellationToken);
        var ids = defs.Select(d => d.Id).ToList();
        var qCounts = await _db.FormQuestions.AsNoTracking().Where(q => ids.Contains(q.DefinitionId))
            .GroupBy(q => q.DefinitionId).Select(g => new { g.Key, C = g.Count() }).ToListAsync(cancellationToken);
        var rCounts = await _db.FormResponses.AsNoTracking()
            .Where(r => ids.Contains(r.DefinitionId) && r.Status == FormResponseStatus.Submitted)
            .GroupBy(r => r.DefinitionId).Select(g => new { g.Key, C = g.Count() }).ToListAsync(cancellationToken);
        return defs.Select(d => new FormDefinitionListItemDto(
            d.Id, d.Code, d.Title, d.Description, d.Status, d.Revision, d.IsArchived,
            qCounts.FirstOrDefault(x => x.Key == d.Id)?.C ?? 0, d.Version,
            rCounts.FirstOrDefault(x => x.Key == d.Id)?.C ?? 0)).ToList();
    }

    public async Task<FormDefinitionDetailDto?> GetAsync(long definitionId, CancellationToken cancellationToken = default)
    {
        var def = await _db.FormDefinitions.AsNoTracking().FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        return def is null ? null : await ToDetailAsync(def, cancellationToken);
    }

    public async Task<FormResult<FormDefinitionDetailDto>> CreateAsync(CreateFormDefinitionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) { return FormResult<FormDefinitionDetailDto>.Invalid("El codigo es obligatorio."); }
        if (string.IsNullOrWhiteSpace(request.Title)) { return FormResult<FormDefinitionDetailDto>.Invalid("El titulo es obligatorio."); }
        var code = request.Code.Trim();
        if (await _db.FormDefinitions.AnyAsync(d => d.Code == code, cancellationToken))
        {
            return FormResult<FormDefinitionDetailDto>.Conflict("Ya existe un formulario con ese codigo.");
        }
        var def = new FormDefinition
        {
            TenantId = _tenantContext.TenantId!.Value,
            Code = code, Title = request.Title.Trim(), Description = Trim(request.Description),
            Status = FormStatus.Draft, Revision = 1
        };
        _db.FormDefinitions.Add(def);
        _audit.Write(actorUserId, "form.crear", nameof(FormDefinition), def, null, new { def.Code, def.Title }, def.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<FormDefinitionDetailDto>.Ok((await ToDetailAsync(def, cancellationToken)));
    }

    public async Task<FormResult<FormDefinitionDetailDto>> UpdateHeaderAsync(long definitionId, UpdateFormDefinitionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var def = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (def is null) { return FormResult<FormDefinitionDetailDto>.NotFound("El formulario no existe."); }
        if (string.IsNullOrWhiteSpace(request.Title)) { return FormResult<FormDefinitionDetailDto>.Invalid("El titulo es obligatorio."); }
        def.Title = request.Title.Trim();
        def.Description = Trim(request.Description);
        return await SaveDefAsync(def, actorUserId, "form.editar", cancellationToken);
    }

    public async Task<FormResult<FormDefinitionDetailDto>> ActivateAsync(long definitionId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var def = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (def is null) { return FormResult<FormDefinitionDetailDto>.NotFound("El formulario no existe."); }

        var questions = await _db.FormQuestions.AsNoTracking().Where(q => q.DefinitionId == def.Id).ToListAsync(cancellationToken);
        if (questions.Count == 0) { return FormResult<FormDefinitionDetailDto>.Invalid("Agrega al menos un campo antes de publicar."); }
        foreach (var q in questions)
        {
            var err = ValidateQuestionStructure(q.ControlType, q.OptionsJson, q.ValidationJson);
            if (err is not null) { return FormResult<FormDefinitionDetailDto>.Invalid($"Campo '{q.Label}': {err}"); }
        }
        def.Status = FormStatus.Active;
        return await SaveDefAsync(def, actorUserId, "form.activar", cancellationToken);
    }

    public async Task<FormResult<FormDefinitionDetailDto>> DeactivateAsync(long definitionId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var def = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (def is null) { return FormResult<FormDefinitionDetailDto>.NotFound("El formulario no existe."); }
        def.Status = FormStatus.Inactive;
        return await SaveDefAsync(def, actorUserId, "form.desactivar", cancellationToken);
    }

    public async Task<FormResult<bool>> SetArchivedAsync(long definitionId, bool archived, long actorUserId, CancellationToken cancellationToken = default)
    {
        var def = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (def is null) { return FormResult<bool>.NotFound("El formulario no existe."); }
        def.IsArchived = archived;
        _audit.Write(actorUserId, "form.archivar", nameof(FormDefinition), def, null, new { archived }, def.TenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    // ---- Contenedores ----

    public async Task<FormResult<FormContainerDto>> AddContainerAsync(long definitionId, SaveFormContainerRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var def = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (def is null) { return FormResult<FormContainerDto>.NotFound("El formulario no existe."); }
        if (string.IsNullOrWhiteSpace(request.Name)) { return FormResult<FormContainerDto>.Invalid("El nombre del contenedor es obligatorio."); }

        var order = await NextContainerOrderAsync(def.Id, request.ParentId, cancellationToken);
        var c = new FormContainer
        {
            TenantId = def.TenantId, DefinitionId = def.Id, Name = request.Name.Trim(),
            ContainerType = request.ContainerType, ParentId = request.ParentId, SortOrder = order,
            Style = Trim(request.Style), TabsJson = request.TabsJson, Width = Clamp12(request.Width),
            IsLocked = request.IsLocked, IsHidden = request.IsHidden, InlineLabels = request.InlineLabels
        };
        _db.FormContainers.Add(c);
        TouchRevision(def);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<FormContainerDto>.Ok(ToContainerDto(c));
    }

    public async Task<FormResult<FormContainerDto>> UpdateContainerAsync(long containerId, SaveFormContainerRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var c = await _db.FormContainers.FirstOrDefaultAsync(x => x.Id == containerId, cancellationToken);
        if (c is null) { return FormResult<FormContainerDto>.NotFound("El contenedor no existe."); }
        if (string.IsNullOrWhiteSpace(request.Name)) { return FormResult<FormContainerDto>.Invalid("El nombre es obligatorio."); }
        c.Name = request.Name.Trim(); c.ContainerType = request.ContainerType; c.Style = Trim(request.Style);
        c.TabsJson = request.TabsJson; c.Width = Clamp12(request.Width);
        c.IsLocked = request.IsLocked; c.IsHidden = request.IsHidden; c.InlineLabels = request.InlineLabels;
        await TouchRevisionByDefAsync(c.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<FormContainerDto>.Ok(ToContainerDto(c));
    }

    public async Task<FormResult<bool>> DeleteContainerAsync(long containerId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var c = await _db.FormContainers.FirstOrDefaultAsync(x => x.Id == containerId, cancellationToken);
        if (c is null) { return FormResult<bool>.NotFound("El contenedor no existe."); }
        // Sus preguntas y sub-contenedores suben al padre (o a la raiz).
        var childQuestions = await _db.FormQuestions.Where(q => q.ContainerId == containerId).ToListAsync(cancellationToken);
        foreach (var q in childQuestions) { q.ContainerId = c.ParentId; }
        var childContainers = await _db.FormContainers.Where(x => x.ParentId == containerId).ToListAsync(cancellationToken);
        foreach (var sc in childContainers) { sc.ParentId = c.ParentId; }
        _db.FormContainers.Remove(c);
        await TouchRevisionByDefAsync(c.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    public async Task<FormResult<bool>> MoveContainerAsync(long containerId, bool moveUp, long actorUserId, CancellationToken cancellationToken = default)
    {
        var c = await _db.FormContainers.FirstOrDefaultAsync(x => x.Id == containerId, cancellationToken);
        if (c is null) { return FormResult<bool>.NotFound("El contenedor no existe."); }
        var siblings = await _db.FormContainers.Where(x => x.DefinitionId == c.DefinitionId && x.ParentId == c.ParentId)
            .OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        SwapNeighbor(siblings, c.Id, moveUp);
        await TouchRevisionByDefAsync(c.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    public async Task<FormResult<bool>> MoveContainerToAsync(long containerId, long? parentId, int index, long actorUserId, CancellationToken cancellationToken = default)
    {
        var c = await _db.FormContainers.FirstOrDefaultAsync(x => x.Id == containerId, cancellationToken);
        if (c is null) { return FormResult<bool>.NotFound("El contenedor no existe."); }
        if (parentId == containerId) { return FormResult<bool>.Invalid("Un contenedor no puede ser su propio padre."); }
        c.ParentId = parentId;
        c.SortOrder = index;
        await RenumberContainersAsync(c.DefinitionId, parentId, cancellationToken);
        await TouchRevisionByDefAsync(c.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    // ---- Preguntas ----

    public async Task<FormResult<FormQuestionDto>> AddQuestionAsync(long definitionId, SaveFormQuestionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var def = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (def is null) { return FormResult<FormQuestionDto>.NotFound("El formulario no existe."); }
        var err = ValidateQuestionRequest(request);
        if (err is not null) { return FormResult<FormQuestionDto>.Invalid(err); }
        var code = request.FieldCode.Trim();
        if (await _db.FormQuestions.AnyAsync(q => q.DefinitionId == def.Id && q.FieldCode == code, cancellationToken))
        {
            return FormResult<FormQuestionDto>.Conflict("Ya existe un campo con ese codigo en el formulario.");
        }
        var order = await NextQuestionOrderAsync(def.Id, request.ContainerId, cancellationToken);
        var width = Clamp12(request.Width);
        var q = new FormQuestion
        {
            TenantId = def.TenantId, DefinitionId = def.Id, ContainerId = request.ContainerId,
            FieldCode = code, Label = request.Label.Trim(), Caption = Trim(request.Caption),
            HelpText = Trim(request.HelpText), ControlType = request.ControlType, OptionsJson = request.OptionsJson,
            Required = request.Required, SortOrder = order, GridCol = GridColFromWidth(width),
            Numeral = Trim(request.Numeral), ValidationJson = request.ValidationJson, Width = width,
            PlaceholderText = Trim(request.PlaceholderText), DefaultValue = request.DefaultValue,
            IsLocked = request.IsLocked, IsHidden = request.IsHidden, Format = Trim(request.Format)
        };
        _db.FormQuestions.Add(q);
        TouchRevision(def);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<FormQuestionDto>.Ok(ToQuestionDto(q));
    }

    public async Task<FormResult<FormQuestionDto>> UpdateQuestionAsync(long questionId, SaveFormQuestionRequest request, long actorUserId, CancellationToken cancellationToken = default)
    {
        var q = await _db.FormQuestions.FirstOrDefaultAsync(x => x.Id == questionId, cancellationToken);
        if (q is null) { return FormResult<FormQuestionDto>.NotFound("El campo no existe."); }
        var err = ValidateQuestionRequest(request);
        if (err is not null) { return FormResult<FormQuestionDto>.Invalid(err); }
        var code = request.FieldCode.Trim();
        if (await _db.FormQuestions.AnyAsync(x => x.DefinitionId == q.DefinitionId && x.FieldCode == code && x.Id != q.Id, cancellationToken))
        {
            return FormResult<FormQuestionDto>.Conflict("Ya existe un campo con ese codigo en el formulario.");
        }
        var width = Clamp12(request.Width);
        q.ContainerId = request.ContainerId; q.FieldCode = code; q.Label = request.Label.Trim();
        q.Caption = Trim(request.Caption); q.HelpText = Trim(request.HelpText); q.ControlType = request.ControlType;
        q.OptionsJson = request.OptionsJson; q.Required = request.Required; q.GridCol = GridColFromWidth(width);
        q.Numeral = Trim(request.Numeral); q.ValidationJson = request.ValidationJson; q.Width = width;
        q.PlaceholderText = Trim(request.PlaceholderText); q.DefaultValue = request.DefaultValue;
        q.IsLocked = request.IsLocked; q.IsHidden = request.IsHidden; q.Format = Trim(request.Format);
        await TouchRevisionByDefAsync(q.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<FormQuestionDto>.Ok(ToQuestionDto(q));
    }

    public async Task<FormResult<bool>> DeleteQuestionAsync(long questionId, long actorUserId, CancellationToken cancellationToken = default)
    {
        var q = await _db.FormQuestions.FirstOrDefaultAsync(x => x.Id == questionId, cancellationToken);
        if (q is null) { return FormResult<bool>.NotFound("El campo no existe."); }
        _db.FormQuestions.Remove(q);
        await TouchRevisionByDefAsync(q.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    public async Task<FormResult<bool>> MoveQuestionAsync(long questionId, bool moveUp, long actorUserId, CancellationToken cancellationToken = default)
    {
        var q = await _db.FormQuestions.FirstOrDefaultAsync(x => x.Id == questionId, cancellationToken);
        if (q is null) { return FormResult<bool>.NotFound("El campo no existe."); }
        var siblings = await _db.FormQuestions.Where(x => x.DefinitionId == q.DefinitionId && x.ContainerId == q.ContainerId)
            .OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        SwapNeighbor(siblings, q.Id, moveUp);
        await TouchRevisionByDefAsync(q.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    public async Task<FormResult<bool>> MoveQuestionToAsync(long questionId, long? containerId, int index, long actorUserId, CancellationToken cancellationToken = default)
    {
        var q = await _db.FormQuestions.FirstOrDefaultAsync(x => x.Id == questionId, cancellationToken);
        if (q is null) { return FormResult<bool>.NotFound("El campo no existe."); }
        q.ContainerId = containerId;
        q.SortOrder = index;
        await RenumberQuestionsAsync(q.DefinitionId, containerId, cancellationToken);
        await TouchRevisionByDefAsync(q.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    // ---- Helpers ----

    private async Task<FormResult<FormDefinitionDetailDto>> SaveDefAsync(FormDefinition def, long actorUserId, string action, CancellationToken cancellationToken)
    {
        _audit.Write(actorUserId, action, nameof(FormDefinition), def, null, new { def.Title, def.Status }, def.TenantId);
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return FormResult<FormDefinitionDetailDto>.Conflict("El formulario fue modificado por otra persona. Recarga."); }
        return FormResult<FormDefinitionDetailDto>.Ok((await ToDetailAsync(def, cancellationToken)));
    }

    private static void TouchRevision(FormDefinition def)
    {
        if (def.Status == FormStatus.Active) { def.Revision++; }
    }

    private async Task TouchRevisionByDefAsync(long definitionId, CancellationToken cancellationToken)
    {
        var def = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (def is not null) { TouchRevision(def); }
    }

    private async Task<int> NextContainerOrderAsync(long defId, long? parentId, CancellationToken ct)
        => (await _db.FormContainers.Where(x => x.DefinitionId == defId && x.ParentId == parentId)
            .Select(x => (int?)x.SortOrder).MaxAsync(ct) ?? -1) + 1;

    private async Task<int> NextQuestionOrderAsync(long defId, long? containerId, CancellationToken ct)
        => (await _db.FormQuestions.Where(x => x.DefinitionId == defId && x.ContainerId == containerId)
            .Select(x => (int?)x.SortOrder).MaxAsync(ct) ?? -1) + 1;

    private static void SwapNeighbor<T>(List<T> ordered, long id, bool up) where T : class
    {
        var idx = ordered.FindIndex(e => (long)e.GetType().GetProperty("Id")!.GetValue(e)! == id);
        if (idx < 0) { return; }
        var target = up ? idx - 1 : idx + 1;
        if (target < 0 || target >= ordered.Count) { return; }
        var a = ordered[idx]; var b = ordered[target];
        var pa = a.GetType().GetProperty("SortOrder")!; var pb = b.GetType().GetProperty("SortOrder")!;
        var va = (int)pa.GetValue(a)!; var vb = (int)pb.GetValue(b)!;
        pa.SetValue(a, vb); pb.SetValue(b, va);
    }

    private async Task RenumberContainersAsync(long defId, long? parentId, CancellationToken ct)
    {
        var group = await _db.FormContainers.Where(x => x.DefinitionId == defId && x.ParentId == parentId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        for (var i = 0; i < group.Count; i++) { group[i].SortOrder = i; }
    }

    private async Task RenumberQuestionsAsync(long defId, long? containerId, CancellationToken ct)
    {
        var group = await _db.FormQuestions.Where(x => x.DefinitionId == defId && x.ContainerId == containerId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(ct);
        for (var i = 0; i < group.Count; i++) { group[i].SortOrder = i; }
    }

    private async Task<FormDefinitionDetailDto> ToDetailAsync(FormDefinition def, CancellationToken ct)
    {
        var containers = await _db.FormContainers.AsNoTracking().Where(c => c.DefinitionId == def.Id)
            .OrderBy(c => c.SortOrder).ToListAsync(ct);
        var questions = await _db.FormQuestions.AsNoTracking().Where(q => q.DefinitionId == def.Id)
            .OrderBy(q => q.SortOrder).ToListAsync(ct);
        return new FormDefinitionDetailDto(
            def.Id, def.Code, def.Title, def.Description, def.Status, def.Revision, def.IsArchived, def.Version,
            containers.Select(ToContainerDto).ToList(), questions.Select(ToQuestionDto).ToList());
    }

    private static FormContainerDto ToContainerDto(FormContainer c)
        => new(c.Id, c.Name, c.ContainerType, c.ParentId, c.SortOrder, c.Style, c.TabsJson, c.Width, c.IsLocked, c.IsHidden, c.InlineLabels);

    private static FormQuestionDto ToQuestionDto(FormQuestion q)
        => new(q.Id, q.ContainerId, q.FieldCode, q.Label, q.Caption, q.HelpText, q.ControlType, q.OptionsJson,
            q.Required, q.SortOrder, q.GridCol, q.Numeral, q.ValidationJson, q.Width, q.PlaceholderText,
            q.DefaultValue, q.IsLocked, q.IsHidden, q.Format);

    private static string? ValidateQuestionRequest(SaveFormQuestionRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.FieldCode)) { return "El codigo del campo es obligatorio."; }
        if (!Regex.IsMatch(r.FieldCode.Trim(), "^[a-zA-Z][a-zA-Z0-9_]*$")) { return "El codigo del campo debe empezar por letra y usar solo letras, numeros o guion bajo."; }
        if (string.IsNullOrWhiteSpace(r.Label)) { return "La etiqueta es obligatoria."; }
        return ValidateQuestionStructure(r.ControlType, r.OptionsJson, r.ValidationJson);
    }

    private static string? ValidateQuestionStructure(FormControlType type, string? optionsJson, string? validationJson)
    {
        if (type is FormControlType.Select or FormControlType.MultiCheck or FormControlType.Radio)
        {
            if (FormFieldValidator.ParseOptions(optionsJson).Count == 0) { return "Debe tener al menos una opcion."; }
        }
        var rules = FormFieldValidator.ParseRules(validationJson);
        if (!string.IsNullOrEmpty(rules?.Pattern))
        {
            try { _ = Regex.Match("", rules.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100)); }
            catch (ArgumentException) { return "El patron de validacion no es una expresion regular valida."; }
        }
        return null;
    }

    private static int Clamp12(int w) => w < 1 ? 1 : w > 12 ? 12 : w;
    private static string GridColFromWidth(int w) => w >= 12 ? "col-12" : $"col-md-{w}";
    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
