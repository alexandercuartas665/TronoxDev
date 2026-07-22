using Tronox.Domain.Enums;

namespace Tronox.Application.Organization;

/// <summary>
/// Validacion PURA de un nodo del arbol organizacional (RQ01 - RF03/RF04, ADR-003): sin EF,
/// testeable sin base de datos. Devuelve el mensaje de error o null si el nodo es valido.
///
/// Lo que NO se valida aqui (necesita base de datos y vive en OrgUnitService): existencia del
/// fondo, del padre, de la sucesora y del usuario ocupante; y la unicidad del codigo entre
/// hermanos.
/// </summary>
public static class OrgStructureRules
{
    public const int MaxNombre = 200;
    public const int MaxCodigo = 20;
    public const int MaxDescripcion = 600;

    /// <summary>
    /// Reglas por clasificador:
    ///
    /// Dependencia (RF03): fondo obligatorio, codigo obligatorio (20), nombre (200),
    ///   VigenteDesde obligatoria y VigenteHasta -- si viene -- no anterior a VigenteDesde.
    /// Cargo (RF04): NivelJerarquico obligatorio; CodigoCargo y CodigoDafp opcionales (20).
    ///   El cargo es METADATO ORGANIZACIONAL: no otorga permisos (esos salen de RF05).
    /// Funcionario: exige el usuario del tenant que ocupa el puesto.
    ///
    /// Los atributos de un clasificador que no corresponden se IGNORAN (el servicio los
    /// persiste en null), no se rechazan: asi cambiar el clasificador de un nodo no obliga a
    /// limpiar el formulario campo por campo.
    /// </summary>
    public static string? ValidateNode(
        OrgUnitClassifier classifier,
        string? nombre,
        string? descripcion,
        long? fondoId,
        string? codigo,
        DateOnly? vigenteDesde,
        DateOnly? vigenteHasta,
        string? codigoCargo,
        string? codigoDafp,
        NivelJerarquico? nivelJerarquico,
        long? tenantUserId)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return "El nombre es obligatorio.";
        }
        if (nombre.Trim().Length > MaxNombre)
        {
            return $"El nombre no puede superar {MaxNombre} caracteres.";
        }
        if (descripcion is { } d && d.Trim().Length > MaxDescripcion)
        {
            return $"La descripcion no puede superar {MaxDescripcion} caracteres.";
        }

        switch (classifier)
        {
            case OrgUnitClassifier.Dependencia:
                // fondo_id es OBLIGATORIO solo aqui: es el nodo que cuelga de un fondo y
                // hereda el principio de procedencia (RF03).
                if (fondoId is null)
                {
                    return "Una Dependencia requiere el fondo documental al que pertenece.";
                }
                if (string.IsNullOrWhiteSpace(codigo))
                {
                    return "Una Dependencia requiere codigo.";
                }
                if (codigo.Trim().Length > MaxCodigo)
                {
                    return $"El codigo no puede superar {MaxCodigo} caracteres.";
                }
                if (vigenteDesde is null)
                {
                    return "Una Dependencia requiere la fecha de inicio de vigencia.";
                }
                if (vigenteHasta is DateOnly hasta && hasta < vigenteDesde.Value)
                {
                    return "La fecha de fin de vigencia no puede ser anterior a la de inicio.";
                }
                break;

            case OrgUnitClassifier.Cargo:
                if (nivelJerarquico is null)
                {
                    return "Un Cargo requiere nivel jerarquico.";
                }
                if (codigoCargo is { } cc && cc.Trim().Length > MaxCodigo)
                {
                    return $"El codigo del cargo no puede superar {MaxCodigo} caracteres.";
                }
                // Solo aplica a entidades publicas, por eso es opcional.
                if (codigoDafp is { } cd && cd.Trim().Length > MaxCodigo)
                {
                    return $"El codigo DAFP no puede superar {MaxCodigo} caracteres.";
                }
                break;

            case OrgUnitClassifier.Funcionario:
                if (tenantUserId is null)
                {
                    return "Un Funcionario requiere el usuario del tenant que ocupa el puesto.";
                }
                break;
        }

        return null;
    }

    /// <summary>
    /// Coherencia estructural padre -&gt; hijo. Jerarquia SUAVE (la profundidad de dependencias
    /// es ilimitada, asi que una Dependencia puede colgar de otra Dependencia o ser raiz):
    /// solo se prohibe lo que romperia el resolver de dependencia.
    /// </summary>
    public static string? ValidateParent(OrgUnitClassifier classifier, OrgUnitClassifier? parentClassifier)
        => (classifier, parentClassifier) switch
        {
            // Una Dependencia solo cuelga de otra Dependencia (o es raiz): colgarla de un
            // Cargo haria que el resolver devolviera la dependencia equivocada.
            (OrgUnitClassifier.Dependencia, OrgUnitClassifier.Cargo or OrgUnitClassifier.Funcionario)
                => "Una Dependencia solo puede colgar de otra Dependencia (o ser raiz).",
            // Un Cargo cuelga de una Dependencia. Colgarlo de la raiz se PERMITE a proposito:
            // es el caso fail-closed del Addendum (sin Dependencia encima = sin visibilidad).
            (OrgUnitClassifier.Cargo, OrgUnitClassifier.Cargo or OrgUnitClassifier.Funcionario)
                => "Un Cargo debe colgar de una Dependencia (o ser raiz).",
            (OrgUnitClassifier.Funcionario, not null and not OrgUnitClassifier.Cargo)
                => "Un Funcionario debe colgar de un Cargo.",
            _ => null
        };
}
