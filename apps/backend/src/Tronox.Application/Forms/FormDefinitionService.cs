using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Tronox.Application.Common;
using Tronox.Domain.Entities;
using Tronox.Domain.Enums;

namespace Tronox.Application.Forms;

/// <summary>
/// Implementacion del CRUD de definiciones de formulario (RQ08, port ECOREX / ADR-0015). Reglas clave:
/// FieldCode unico por definicion y con formato de identificador; Select/MultiCheck/Radio/GridDetail
/// exigen al menos una opcion parseable; el pattern de ValidationJson debe compilar; y toda mutacion
/// estructural sobre una definicion Active incrementa Revision (version de negocio, distinta del token
/// de concurrencia Version/IVersioned).
///
/// Adaptaciones TRONOX: Guid->long en ids/FKs; el nodo de menu del modulo (SetModule) queda DIFERIDO
/// (se marca IsModule sin crear MenuNode); el vinculo a nodos de flujo BPMN no se porta aqui.
/// </summary>
public sealed partial class FormDefinitionService : IFormDefinitionService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantContext _tenantContext;

    // FieldCode: inicia con letra y usa solo letras, numeros y guion bajo (clave del documento JSON).
    [GeneratedRegex("^[a-zA-Z][a-zA-Z0-9_]*$")]
    private static partial Regex FieldCodeRegex();

    public FormDefinitionService(IApplicationDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<FormDefinitionListItemDto>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var rows = await _db.FormDefinitions.AsNoTracking()
            .Where(d => includeArchived || !d.IsArchived)
            .OrderBy(d => d.Code)
            .Select(d => new
            {
                d.Id,
                d.Code,
                d.Title,
                d.Description,
                d.Status,
                d.Revision,
                d.IsArchived,
                d.Version,
                QuestionCount = _db.FormQuestions.Count(q => q.DefinitionId == d.Id),
                // KPIs del indice: respuestas enviadas y reglas condicionales vinculadas por definicion.
                ResponseCount = _db.FormResponses.Count(r => r.DefinitionId == d.Id),
                RuleCount = _db.FormFieldConditions.Count(fc => fc.DefinitionId == d.Id)
            })
            .ToListAsync(cancellationToken);
        return rows
            .Select(d => new FormDefinitionListItemDto(
                d.Id, d.Code, d.Title, d.Description, d.Status, d.Revision, d.IsArchived,
                d.QuestionCount, d.Version, d.ResponseCount, d.RuleCount))
            .ToList();
    }

    public async Task<FormDefinitionDetailDto?> GetAsync(long definitionId, CancellationToken cancellationToken = default)
    {
        var definition = await _db.FormDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        return definition is null ? null : await BuildDetailAsync(definition, cancellationToken);
    }

    public async Task<FormResult<FormDefinitionDetailDto>> CreateAsync(CreateFormDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        if (_tenantContext.TenantId is not long tenantId)
        {
            return FormResult<FormDefinitionDetailDto>.Invalid("No hay tenant activo.");
        }
        var code = (request.Code ?? "").Trim().ToUpperInvariant();
        if (code.Length is 0 or > 20)
        {
            return FormResult<FormDefinitionDetailDto>.Invalid("El codigo es obligatorio (maximo 20 caracteres).");
        }
        var title = (request.Title ?? "").Trim();
        if (title.Length is 0 or > 200)
        {
            return FormResult<FormDefinitionDetailDto>.Invalid("El titulo es obligatorio (maximo 200 caracteres).");
        }
        if (await _db.FormDefinitions.AnyAsync(d => d.Code == code, cancellationToken))
        {
            return FormResult<FormDefinitionDetailDto>.Invalid($"Ya existe un formulario con el codigo {code}.");
        }

        var definition = new FormDefinition
        {
            TenantId = tenantId,
            Code = code,
            Title = title,
            Description = Normalize(request.Description),
            Status = FormStatus.Draft,
            Revision = 1
        };
        _db.FormDefinitions.Add(definition);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<FormDefinitionDetailDto>.Ok(await BuildDetailAsync(definition, cancellationToken));
    }

    public async Task<FormResult<FormDefinitionDetailDto>> UpdateHeaderAsync(long definitionId, UpdateFormDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var definition = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (definition is null)
        {
            return FormResult<FormDefinitionDetailDto>.NotFound("Formulario no encontrado.");
        }
        var title = (request.Title ?? "").Trim();
        if (title.Length is 0 or > 200)
        {
            return FormResult<FormDefinitionDetailDto>.Invalid("El titulo es obligatorio (maximo 200 caracteres).");
        }
        if (definition.Version != request.Version)
        {
            return FormResult<FormDefinitionDetailDto>.Conflict(ConflictMessage);
        }

        definition.Title = title;
        definition.Description = Normalize(request.Description);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return FormResult<FormDefinitionDetailDto>.Conflict(ConflictMessage);
        }
        return FormResult<FormDefinitionDetailDto>.Ok(await BuildDetailAsync(definition, cancellationToken));
    }

    public async Task<FormResult<FormDefinitionDetailDto>> ActivateAsync(long definitionId, CancellationToken cancellationToken = default)
    {
        var definition = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (definition is null)
        {
            return FormResult<FormDefinitionDetailDto>.NotFound("Formulario no encontrado.");
        }
        if (definition.IsArchived)
        {
            return FormResult<FormDefinitionDetailDto>.Invalid("El formulario esta archivado.");
        }
        if (definition.Status == FormStatus.Active)
        {
            return FormResult<FormDefinitionDetailDto>.Ok(await BuildDetailAsync(definition, cancellationToken));
        }

        // Validacion estructural completa antes de publicar.
        var questions = await _db.FormQuestions.AsNoTracking()
            .Where(q => q.DefinitionId == definition.Id)
            .ToListAsync(cancellationToken);
        if (!questions.Any(q => !FormFieldValidator.IsNonInput(q.ControlType)))
        {
            return FormResult<FormDefinitionDetailDto>.Invalid("El formulario necesita al menos una pregunta que capture datos.");
        }
        foreach (var question in questions)
        {
            var structural = ValidateQuestionStructure(question.ControlType, question.OptionsJson, question.ValidationJson);
            if (structural is not null)
            {
                return FormResult<FormDefinitionDetailDto>.Invalid($"Pregunta '{question.FieldCode}': {structural}");
            }
        }

        definition.Status = FormStatus.Active;
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<FormDefinitionDetailDto>.Ok(await BuildDetailAsync(definition, cancellationToken));
    }

    public async Task<FormResult<FormDefinitionDetailDto>> DeactivateAsync(long definitionId, CancellationToken cancellationToken = default)
    {
        var definition = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (definition is null)
        {
            return FormResult<FormDefinitionDetailDto>.NotFound("Formulario no encontrado.");
        }
        if (definition.Status != FormStatus.Active)
        {
            return FormResult<FormDefinitionDetailDto>.Invalid("Solo un formulario activo puede desactivarse.");
        }
        definition.Status = FormStatus.Inactive;
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<FormDefinitionDetailDto>.Ok(await BuildDetailAsync(definition, cancellationToken));
    }

    public async Task<FormResult<bool>> SetArchivedAsync(long definitionId, bool archived, CancellationToken cancellationToken = default)
    {
        var definition = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (definition is null)
        {
            return FormResult<bool>.NotFound("Formulario no encontrado.");
        }

        // Archivar un formulario que alguien esta usando deja ese proceso sin su formulario. Por eso se
        // bloquea y se dice QUIEN lo usa, en vez de romperlo en silencio. Las respuestas NO bloquean:
        // archivar no las toca y es reversible.
        if (archived)
        {
            var usos = await DescribeUsagesAsync(definitionId, cancellationToken);
            if (usos.Count > 0)
            {
                return FormResult<bool>.Invalid($"No se puede archivar: {string.Join("; ", usos)}.");
            }
        }

        definition.IsArchived = archived;
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    /// <summary>
    /// Enumera en lenguaje llano quien esta usando el formulario. Vacio = nadie lo usa. En TRONOX cubre
    /// las dos formas que existen hoy: pasos de flujo BPMN y ser subformulario de otro. DIFERIDO: usos
    /// por conceptos de actividad y modal de terceros (entidades propias de ECOREX, podadas).
    /// </summary>
    private async Task<List<string>> DescribeUsagesAsync(long definitionId, CancellationToken cancellationToken)
    {
        var usos = new List<string>();

        var nodos = await _db.WorkflowNodeForms.CountAsync(f => f.DefinitionId == definitionId, cancellationToken);
        if (nodos > 0)
        {
            usos.Add($"esta asignado a {nodos} paso(s) de flujo");
        }

        var padres = await _db.FormQuestions
            .Where(q => q.SubformDefinitionId == definitionId)
            .Select(q => q.Definition!.Title)
            .Distinct()
            .Take(4)
            .ToListAsync(cancellationToken);
        if (padres.Count > 0)
        {
            usos.Add($"es subformulario de {string.Join(", ", padres)}");
        }

        return usos;
    }

    // ---- Contenedores ----

    public async Task<FormResult<FormContainerDto>> AddContainerAsync(long definitionId, SaveFormContainerRequest request, CancellationToken cancellationToken = default)
    {
        var definition = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (definition is null)
        {
            return FormResult<FormContainerDto>.NotFound("Formulario no encontrado.");
        }
        var name = (request.Name ?? "").Trim();
        if (name.Length is 0 or > 150)
        {
            return FormResult<FormContainerDto>.Invalid("El nombre del contenedor es obligatorio (maximo 150 caracteres).");
        }
        if (request.ParentId is long parentId
            && !await _db.FormContainers.AnyAsync(c => c.Id == parentId && c.DefinitionId == definitionId, cancellationToken))
        {
            return FormResult<FormContainerDto>.Invalid("El contenedor padre no pertenece al formulario.");
        }

        var maxOrder = await _db.FormContainers
            .Where(c => c.DefinitionId == definitionId && c.ParentId == request.ParentId)
            .MaxAsync(c => (int?)c.SortOrder, cancellationToken) ?? -1;
        var container = new FormContainer
        {
            TenantId = definition.TenantId,
            DefinitionId = definitionId,
            Name = name,
            ContainerType = request.ContainerType,
            ParentId = request.ParentId,
            SortOrder = maxOrder + 1,
            Style = Normalize(request.Style),
            TabsJson = Normalize(request.TabsJson),
            Width = Math.Clamp(request.Width, 1, 12),
            IsLocked = request.IsLocked,
            IsHidden = request.IsHidden,
            InlineLabels = request.InlineLabels
        };
        _db.FormContainers.Add(container);
        TouchRevision(definition);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<FormContainerDto>.Ok(ToDto(container));
    }

    public async Task<FormResult<FormContainerDto>> UpdateContainerAsync(long containerId, SaveFormContainerRequest request, CancellationToken cancellationToken = default)
    {
        var container = await _db.FormContainers.FirstOrDefaultAsync(c => c.Id == containerId, cancellationToken);
        if (container is null)
        {
            return FormResult<FormContainerDto>.NotFound("Contenedor no encontrado.");
        }
        var name = (request.Name ?? "").Trim();
        if (name.Length is 0 or > 150)
        {
            return FormResult<FormContainerDto>.Invalid("El nombre del contenedor es obligatorio (maximo 150 caracteres).");
        }
        if (request.ParentId == containerId)
        {
            return FormResult<FormContainerDto>.Invalid("Un contenedor no puede ser su propio padre.");
        }
        if (request.ParentId is long parentId)
        {
            var containers = await _db.FormContainers.AsNoTracking()
                .Where(c => c.DefinitionId == container.DefinitionId)
                .ToListAsync(cancellationToken);
            if (containers.All(c => c.Id != parentId))
            {
                return FormResult<FormContainerDto>.Invalid("El contenedor padre no pertenece al formulario.");
            }
            // El nuevo padre no puede ser un descendiente (evita ciclos en el arbol).
            var byId = containers.ToDictionary(c => c.Id);
            var cursor = (long?)parentId;
            while (cursor is long cid && byId.TryGetValue(cid, out var node))
            {
                if (node.Id == containerId)
                {
                    return FormResult<FormContainerDto>.Invalid("El contenedor padre no puede ser un descendiente.");
                }
                cursor = node.ParentId;
            }
        }

        container.Name = name;
        container.ContainerType = request.ContainerType;
        container.ParentId = request.ParentId;
        container.Style = Normalize(request.Style);
        container.TabsJson = Normalize(request.TabsJson);
        container.Width = Math.Clamp(request.Width, 1, 12);
        container.IsLocked = request.IsLocked;
        container.IsHidden = request.IsHidden;
        container.InlineLabels = request.InlineLabels;
        await TouchRevisionAsync(container.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<FormContainerDto>.Ok(ToDto(container));
    }

    public async Task<FormResult<bool>> DeleteContainerAsync(long containerId, CancellationToken cancellationToken = default)
    {
        var container = await _db.FormContainers.FirstOrDefaultAsync(c => c.Id == containerId, cancellationToken);
        if (container is null)
        {
            return FormResult<bool>.NotFound("Contenedor no encontrado.");
        }

        // Reubicacion explicita (las FKs son NO ACTION): preguntas y sub-contenedores pasan al padre del
        // contenedor borrado (o a la raiz).
        var questions = await _db.FormQuestions
            .Where(q => q.ContainerId == containerId)
            .ToListAsync(cancellationToken);
        foreach (var question in questions)
        {
            question.ContainerId = container.ParentId;
        }
        var children = await _db.FormContainers
            .Where(c => c.ParentId == containerId)
            .ToListAsync(cancellationToken);
        foreach (var child in children)
        {
            child.ParentId = container.ParentId;
        }

        _db.FormContainers.Remove(container);
        await TouchRevisionAsync(container.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    public async Task<FormResult<bool>> MoveContainerAsync(long containerId, bool moveUp, CancellationToken cancellationToken = default)
    {
        var container = await _db.FormContainers.FirstOrDefaultAsync(c => c.Id == containerId, cancellationToken);
        if (container is null)
        {
            return FormResult<bool>.NotFound("Contenedor no encontrado.");
        }
        var siblings = await _db.FormContainers
            .Where(c => c.DefinitionId == container.DefinitionId && c.ParentId == container.ParentId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
        if (!Swap(siblings, container.Id, moveUp))
        {
            return FormResult<bool>.Ok(false);
        }
        await TouchRevisionAsync(container.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    /// <summary>
    /// Drag and drop del constructor (ADR-0021): mueve el contenedor a otro padre (o a la raiz) en la
    /// posicion index, validando pertenencia y ciclos, y renumera hermanos.
    /// </summary>
    public async Task<FormResult<bool>> MoveContainerToAsync(long containerId, long? parentId, int index, CancellationToken cancellationToken = default)
    {
        var container = await _db.FormContainers.FirstOrDefaultAsync(c => c.Id == containerId, cancellationToken);
        if (container is null)
        {
            return FormResult<bool>.NotFound("Contenedor no encontrado.");
        }
        if (parentId == containerId)
        {
            return FormResult<bool>.Invalid("Un contenedor no puede ser su propio padre.");
        }
        if (parentId is long target)
        {
            var containers = await _db.FormContainers.AsNoTracking()
                .Where(c => c.DefinitionId == container.DefinitionId)
                .ToListAsync(cancellationToken);
            if (containers.All(c => c.Id != target))
            {
                return FormResult<bool>.Invalid("El contenedor padre no pertenece al formulario.");
            }
            var byId = containers.ToDictionary(c => c.Id);
            var cursor = (long?)target;
            while (cursor is long cid && byId.TryGetValue(cid, out var node))
            {
                if (node.Id == containerId)
                {
                    return FormResult<bool>.Invalid("El contenedor padre no puede ser un descendiente.");
                }
                cursor = node.ParentId;
            }
        }

        var oldParentId = container.ParentId;
        var oldSiblings = await _db.FormContainers
            .Where(c => c.DefinitionId == container.DefinitionId && c.ParentId == oldParentId && c.Id != containerId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
        var newSiblings = oldParentId == parentId
            ? oldSiblings
            : await _db.FormContainers
                .Where(c => c.DefinitionId == container.DefinitionId && c.ParentId == parentId && c.Id != containerId)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
                .ToListAsync(cancellationToken);

        container.ParentId = parentId;
        for (var i = 0; i < oldSiblings.Count; i++)
        {
            oldSiblings[i].SortOrder = i;
        }
        var insertAt = Math.Clamp(index, 0, newSiblings.Count);
        newSiblings.Insert(insertAt, container);
        for (var i = 0; i < newSiblings.Count; i++)
        {
            newSiblings[i].SortOrder = i;
        }
        await TouchRevisionAsync(container.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    // ---- Preguntas ----

    public async Task<FormResult<FormQuestionDto>> AddQuestionAsync(long definitionId, SaveFormQuestionRequest request, CancellationToken cancellationToken = default)
    {
        var definition = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (definition is null)
        {
            return FormResult<FormQuestionDto>.NotFound("Formulario no encontrado.");
        }
        var error = await ValidateQuestionRequestAsync(definitionId, request, existingQuestionId: null, cancellationToken);
        if (error is not null)
        {
            return FormResult<FormQuestionDto>.Invalid(error);
        }

        var maxOrder = await _db.FormQuestions
            .Where(q => q.DefinitionId == definitionId && q.ContainerId == request.ContainerId)
            .MaxAsync(q => (int?)q.SortOrder, cancellationToken) ?? -1;
        var question = new FormQuestion
        {
            TenantId = definition.TenantId,
            DefinitionId = definitionId,
            SortOrder = maxOrder + 1
        };
        ApplyRequest(question, request);
        _db.FormQuestions.Add(question);
        TouchRevision(definition);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<FormQuestionDto>.Ok(ToDto(question));
    }

    public async Task<FormResult<FormQuestionDto>> UpdateQuestionAsync(long questionId, SaveFormQuestionRequest request, CancellationToken cancellationToken = default)
    {
        var question = await _db.FormQuestions.FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken);
        if (question is null)
        {
            return FormResult<FormQuestionDto>.NotFound("Pregunta no encontrada.");
        }
        var error = await ValidateQuestionRequestAsync(question.DefinitionId, request, questionId, cancellationToken);
        if (error is not null)
        {
            return FormResult<FormQuestionDto>.Invalid(error);
        }

        var containerChanged = question.ContainerId != request.ContainerId;
        ApplyRequest(question, request);
        if (containerChanged)
        {
            var maxOrder = await _db.FormQuestions
                .Where(q => q.DefinitionId == question.DefinitionId && q.ContainerId == request.ContainerId && q.Id != questionId)
                .MaxAsync(q => (int?)q.SortOrder, cancellationToken) ?? -1;
            question.SortOrder = maxOrder + 1;
        }
        await TouchRevisionAsync(question.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<FormQuestionDto>.Ok(ToDto(question));
    }

    public async Task<FormResult<bool>> DeleteQuestionAsync(long questionId, CancellationToken cancellationToken = default)
    {
        var question = await _db.FormQuestions.FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken);
        if (question is null)
        {
            return FormResult<bool>.NotFound("Pregunta no encontrada.");
        }
        _db.FormQuestions.Remove(question);
        await TouchRevisionAsync(question.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    public async Task<FormResult<bool>> MoveQuestionAsync(long questionId, bool moveUp, CancellationToken cancellationToken = default)
    {
        var question = await _db.FormQuestions.FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken);
        if (question is null)
        {
            return FormResult<bool>.NotFound("Pregunta no encontrada.");
        }
        var siblings = await _db.FormQuestions
            .Where(q => q.DefinitionId == question.DefinitionId && q.ContainerId == question.ContainerId)
            .OrderBy(q => q.SortOrder).ThenBy(q => q.CreatedAt)
            .ToListAsync(cancellationToken);
        if (!Swap(siblings, question.Id, moveUp))
        {
            return FormResult<bool>.Ok(false);
        }
        await TouchRevisionAsync(question.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    /// <summary>
    /// Drag and drop del constructor (ADR-0021): mueve la pregunta a otro contenedor (o a la raiz) en la
    /// posicion index, renumerando ambos grupos de hermanos.
    /// </summary>
    public async Task<FormResult<bool>> MoveQuestionToAsync(long questionId, long? containerId, int index, CancellationToken cancellationToken = default)
    {
        var question = await _db.FormQuestions.FirstOrDefaultAsync(q => q.Id == questionId, cancellationToken);
        if (question is null)
        {
            return FormResult<bool>.NotFound("Pregunta no encontrada.");
        }
        if (containerId is long target
            && !await _db.FormContainers.AnyAsync(c => c.Id == target && c.DefinitionId == question.DefinitionId, cancellationToken))
        {
            return FormResult<bool>.Invalid("El contenedor no pertenece al formulario.");
        }

        var oldContainerId = question.ContainerId;
        var oldSiblings = await _db.FormQuestions
            .Where(q => q.DefinitionId == question.DefinitionId && q.ContainerId == oldContainerId && q.Id != questionId)
            .OrderBy(q => q.SortOrder).ThenBy(q => q.CreatedAt)
            .ToListAsync(cancellationToken);
        var newSiblings = oldContainerId == containerId
            ? oldSiblings
            : await _db.FormQuestions
                .Where(q => q.DefinitionId == question.DefinitionId && q.ContainerId == containerId && q.Id != questionId)
                .OrderBy(q => q.SortOrder).ThenBy(q => q.CreatedAt)
                .ToListAsync(cancellationToken);

        question.ContainerId = containerId;
        for (var i = 0; i < oldSiblings.Count; i++)
        {
            oldSiblings[i].SortOrder = i;
        }
        var insertAt = Math.Clamp(index, 0, newSiblings.Count);
        newSiblings.Insert(insertAt, question);
        for (var i = 0; i < newSiblings.Count; i++)
        {
            newSiblings[i].SortOrder = i;
        }
        await TouchRevisionAsync(question.DefinitionId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return FormResult<bool>.Ok(true);
    }

    // ---- Transaccionalidad y modulo ----

    public async Task<FormResult<FormDefinitionDetailDto>> SetTransactionalAsync(
        long definitionId, SetFormTransactionalRequest request, CancellationToken cancellationToken = default)
    {
        var definition = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (definition is null)
        {
            return FormResult<FormDefinitionDetailDto>.NotFound("Formulario no encontrado.");
        }
        definition.IsTransactional = request.IsTransactional;
        definition.IdentityMode = request.IsTransactional ? request.IdentityMode : FormIdentityMode.None;
        definition.IdentitySourceFieldCode =
            request.IsTransactional && request.IdentityMode == FormIdentityMode.NaturalKey
                ? Normalize(request.IdentitySourceFieldCode) : null;
        // Ancho de tarjeta: se guarda desde el mismo panel de Propiedades del formulario.
        definition.CardLayout = request.CardLayout;
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(definitionId, cancellationToken)) is { } dto
            ? FormResult<FormDefinitionDetailDto>.Ok(dto)
            : FormResult<FormDefinitionDetailDto>.NotFound("Formulario no encontrado.");
    }

    /// <summary>
    /// Promueve o retira el formulario como MODULO del sistema (ola F4, doc 01 D1). Guarda icono,
    /// columnas y filtros de la bandeja y activa el formulario si seguia en borrador (un modulo tiene que
    /// poder capturar). El NODO DE MENU queda DIFERIDO en TRONOX: se marca IsModule sin crear el MenuNode.
    /// </summary>
    public async Task<FormResult<FormDefinitionDetailDto>> SetModuleAsync(
        long definitionId, SetFormModuleRequest request, CancellationToken cancellationToken = default)
    {
        var definition = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (definition is null)
        {
            return FormResult<FormDefinitionDetailDto>.NotFound("Formulario no encontrado.");
        }

        if (request.IsModule)
        {
            definition.ModuleIcon = Normalize(request.Icon);
            // Columnas y filtros de la bandeja (doc 01 D6): field codes elegidos por el usuario.
            definition.ListColumnsJson = request.ListColumns is { Count: > 0 }
                ? JsonSerializer.Serialize(request.ListColumns) : null;
            definition.FilterFieldsJson = request.FilterFields is { Count: > 0 }
                ? JsonSerializer.Serialize(request.FilterFields) : null;
            // Un modulo del menu tiene que poder capturar: si sigue en borrador, se activa al publicar.
            if (definition.Status != FormStatus.Active)
            {
                definition.Status = FormStatus.Active;
            }
            // TODO(RQ08): crear el nodo de menu del modulo (Kind=Item, Route=/m/{code}) en la vista+grupo
            // elegidos (request.MenuViewId/ParentNodeId/MenuLabel) via el servicio de menu. DIFERIDO: se
            // marca IsModule sin nodo (ModuleMenuNodeId queda null) hasta portar el menu configurable.
            definition.ModuleMenuNodeId = null;
            definition.IsModule = true;
        }
        else
        {
            // TODO(RQ08): borrar el nodo de menu del modulo si existiera, cuando el menu este portado.
            definition.ModuleMenuNodeId = null;
            definition.IsModule = false;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(definitionId, cancellationToken)) is { } dto
            ? FormResult<FormDefinitionDetailDto>.Ok(dto)
            : FormResult<FormDefinitionDetailDto>.NotFound("Formulario no encontrado.");
    }

    // ---- Helpers ----

    private const string ConflictMessage = "Otro usuario modifico el formulario. Recarga e intenta de nuevo.";

    private async Task<FormDefinitionDetailDto> BuildDetailAsync(FormDefinition definition, CancellationToken cancellationToken)
    {
        var containers = await _db.FormContainers.AsNoTracking()
            .Where(c => c.DefinitionId == definition.Id)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
        var questions = await _db.FormQuestions.AsNoTracking()
            .Where(q => q.DefinitionId == definition.Id)
            .OrderBy(q => q.SortOrder).ThenBy(q => q.CreatedAt)
            .ToListAsync(cancellationToken);
        return new FormDefinitionDetailDto(
            definition.Id, definition.Code, definition.Title, definition.Description,
            definition.Status, definition.Revision, definition.IsArchived, definition.Version,
            containers.Select(ToDto).ToList(),
            questions.Select(ToDto).ToList(),
            definition.IsTransactional, definition.IdentityMode, definition.IdentitySourceFieldCode,
            definition.IsModule, definition.ModuleIcon, definition.ListColumnsJson, definition.FilterFieldsJson,
            definition.CardLayout);
    }

    private static FormContainerDto ToDto(FormContainer c)
        => new(c.Id, c.Name, c.ContainerType, c.ParentId, c.SortOrder, c.Style,
            c.TabsJson, c.Width, c.IsLocked, c.IsHidden, c.InlineLabels);

    private static FormQuestionDto ToDto(FormQuestion q)
        => new(q.Id, q.ContainerId, q.FieldCode, q.Label, q.Caption, q.HelpText, q.ControlType,
            q.OptionsJson, q.Required, q.SortOrder, q.GridCol, q.Numeral, q.ValidationJson,
            q.Width, q.PlaceholderText, q.DefaultValue, q.IsLocked, q.IsHidden,
            q.SourceKind, q.SourceRef, q.DisplayField, q.ValueField, q.FilterJson,
            q.AutofillMapJson, q.Presentation, q.CalcExpression, q.Aggregate, q.SubformDefinitionId,
            q.DefaultDynamic, q.Format, q.FieldVisibilityJson, q.CascadeConfigJson);

    private static void ApplyRequest(FormQuestion question, SaveFormQuestionRequest request)
    {
        question.ContainerId = request.ContainerId;
        question.FieldCode = request.FieldCode.Trim();
        question.Label = request.Label.Trim();
        question.Caption = Normalize(request.Caption);
        question.HelpText = Normalize(request.HelpText);
        question.ControlType = request.ControlType;
        question.OptionsJson = Normalize(request.OptionsJson);
        question.Required = request.Required && !FormFieldValidator.IsNonInput(request.ControlType)
            && !FormFieldValidator.IsPlaceholderCapture(request.ControlType);
        // Width (1..12) manda sobre el layout; GridCol se mantiene sincronizado para el renderer
        // bootstrap. Compatibilidad: si Width viene en 12 (default) y GridCol trae una columna
        // parseable, Width se deriva.
        var width = Math.Clamp(request.Width, 1, 12);
        if (width == 12 && ParseGridColWidth(request.GridCol) is int legacy)
        {
            width = legacy;
        }
        question.Width = width;
        question.GridCol = WidthToGridCol(width);
        question.PlaceholderText = Normalize(request.PlaceholderText);
        question.DefaultValue = Normalize(request.DefaultValue);
        question.IsLocked = request.IsLocked;
        question.IsHidden = request.IsHidden;
        question.Numeral = Normalize(request.Numeral);
        question.ValidationJson = Normalize(request.ValidationJson);
        // Origen de datos / lookup (ola F1, doc 01 D4). Con Options (default) el resto queda en null; con
        // una fuente de datos se guardan la fuente, los campos y el mapa de autollenado.
        question.SourceKind = request.SourceKind;
        question.Presentation = request.Presentation;
        if (request.SourceKind == FormSourceKind.Options)
        {
            question.SourceRef = null;
            question.DisplayField = null;
            question.ValueField = null;
            question.FilterJson = null;
            question.AutofillMapJson = null;
        }
        else
        {
            question.SourceRef = Normalize(request.SourceRef);
            question.DisplayField = Normalize(request.DisplayField);
            question.ValueField = Normalize(request.ValueField);
            question.FilterJson = Normalize(request.FilterJson);
            question.AutofillMapJson = Normalize(request.AutofillMapJson);
        }
        // Calculo / agregacion (ola F2, doc 01 D5).
        question.CalcExpression = Normalize(request.CalcExpression);
        question.Aggregate = request.Aggregate;
        // Maestro-detalle (ola F5, doc 01 D7): definicion hija del subformulario.
        question.SubformDefinitionId = request.SubformDefinitionId;
        // Transversales (ola F6, doc 01 D8).
        question.DefaultDynamic = request.DefaultDynamic;
        question.Format = Normalize(request.Format);
        question.FieldVisibilityJson = Normalize(request.FieldVisibilityJson);
        // Configurador en cascada (motor generico): taxonomia de la pregunta.
        question.CascadeConfigJson = Normalize(request.CascadeConfigJson);
    }

    /// <summary>col-12 -> 12, col-md-6 -> 6, col-6 -> 6; null si no parsea.</summary>
    internal static int? ParseGridColWidth(string? gridCol)
    {
        if (string.IsNullOrWhiteSpace(gridCol))
        {
            return null;
        }
        var last = gridCol.Trim().Split('-')[^1];
        return int.TryParse(last, out var n) && n is >= 1 and <= 12 ? n : null;
    }

    internal static string WidthToGridCol(int width)
        => width >= 12 ? "col-12" : $"col-md-{width}";

    private async Task<string?> ValidateQuestionRequestAsync(
        long definitionId, SaveFormQuestionRequest request, long? existingQuestionId,
        CancellationToken cancellationToken)
    {
        var fieldCode = (request.FieldCode ?? "").Trim();
        if (fieldCode.Length is 0 or > 60)
        {
            return "FieldCode es obligatorio (maximo 60 caracteres).";
        }
        if (!FieldCodeRegex().IsMatch(fieldCode))
        {
            return "FieldCode debe iniciar con letra y usar solo letras, numeros y guion bajo.";
        }
        var label = (request.Label ?? "").Trim();
        if (label.Length is 0 or > 500)
        {
            return "La etiqueta es obligatoria (maximo 500 caracteres).";
        }
        if (request.ContainerId is long containerId
            && !await _db.FormContainers.AnyAsync(c => c.Id == containerId && c.DefinitionId == definitionId, cancellationToken))
        {
            return "El contenedor no pertenece al formulario.";
        }
        // FieldCode unico por definicion (clave del documento JSON de respuestas).
        var duplicated = await _db.FormQuestions.AnyAsync(
            q => q.DefinitionId == definitionId && q.FieldCode == fieldCode && q.Id != existingQuestionId,
            cancellationToken);
        if (duplicated)
        {
            return $"Ya existe una pregunta con el FieldCode '{fieldCode}' en este formulario.";
        }
        return ValidateQuestionStructure(request.ControlType, request.OptionsJson, request.ValidationJson);
    }

    /// <summary>Reglas estructurales por tipo: opciones obligatorias y pattern compilable.</summary>
    private static string? ValidateQuestionStructure(FormControlType type, string? optionsJson, string? validationJson)
    {
        // GridDetail (tabla funcional, ADR-0021): OptionsJson define las COLUMNAS con el mismo shape
        // [{id,label}]; exige al menos una columna valida.
        if (type is FormControlType.Select or FormControlType.MultiCheck or FormControlType.Radio
            or FormControlType.GridDetail)
        {
            var options = FormFieldValidator.ParseOptions(optionsJson);
            if (options.Count == 0)
            {
                return type == FormControlType.GridDetail
                    ? "La tabla requiere al menos una columna valida ([{id,label}])."
                    : "Este tipo de control requiere al menos una opcion valida ([{id,label,value}]).";
            }
            if (options.Any(o => string.IsNullOrWhiteSpace(o.Id) || string.IsNullOrWhiteSpace(o.Label)))
            {
                return "Toda opcion necesita id y label.";
            }
            if (options.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count() != options.Count)
            {
                return "Los ids de las opciones deben ser unicos.";
            }
        }
        var rules = FormFieldValidator.ParseRules(validationJson);
        if (!string.IsNullOrWhiteSpace(validationJson) && rules is null)
        {
            return "ValidationJson no es un JSON valido ({minLength,maxLength,pattern,minValue,maxValue}).";
        }
        if (!string.IsNullOrEmpty(rules?.Pattern))
        {
            try
            {
                _ = new Regex(rules.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(250));
            }
            catch (ArgumentException)
            {
                return "El pattern de validacion no es una expresion regular valida.";
            }
        }
        if (rules is { MinLength: int minL, MaxLength: int maxL } && minL > maxL)
        {
            return "minLength no puede ser mayor que maxLength.";
        }
        if (rules is { MinValue: decimal minV, MaxValue: decimal maxV } && minV > maxV)
        {
            return "minValue no puede ser mayor que maxValue.";
        }
        return null;
    }

    /// <summary>
    /// Snapshot logico de version: los cambios estructurales sobre una definicion Active incrementan
    /// Revision (las respuestas ya enviadas conservan su documento tal cual).
    /// </summary>
    private static void TouchRevision(FormDefinition definition)
    {
        if (definition.Status == FormStatus.Active)
        {
            definition.Revision++;
        }
    }

    private async Task TouchRevisionAsync(long definitionId, CancellationToken cancellationToken)
    {
        var definition = await _db.FormDefinitions.FirstOrDefaultAsync(d => d.Id == definitionId, cancellationToken);
        if (definition is not null)
        {
            TouchRevision(definition);
        }
    }

    /// <summary>Reordena por intercambio de SortOrder normalizado. Devuelve false si no hay a donde mover.</summary>
    private static bool Swap<T>(List<T> siblings, long id, bool moveUp) where T : class
    {
        var index = siblings.FindIndex(s => GetId(s) == id);
        if (index < 0)
        {
            return false;
        }
        var target = moveUp ? index - 1 : index + 1;
        if (target < 0 || target >= siblings.Count)
        {
            return false;
        }
        (siblings[index], siblings[target]) = (siblings[target], siblings[index]);
        for (var i = 0; i < siblings.Count; i++)
        {
            SetSortOrder(siblings[i], i);
        }
        return true;

        static long GetId(T item) => item switch
        {
            FormContainer c => c.Id,
            FormQuestion q => q.Id,
            _ => 0L
        };
        static void SetSortOrder(T item, int order)
        {
            switch (item)
            {
                case FormContainer c: c.SortOrder = order; break;
                case FormQuestion q: q.SortOrder = order; break;
            }
        }
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
