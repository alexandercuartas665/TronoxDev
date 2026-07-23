using Tronox.Domain.Enums;

namespace Tronox.Application.MenuConfig;

/// <summary>
/// Reglas PURAS de anidamiento del arbol del menu (sin IO): que Kind puede colgar de que padre.
/// Coherente con el render del sidebar (NavMenu): QuickLink y Section son de primer nivel;
/// Subgroup cuelga de una Section o de otro Subgroup (sub-seccion anidada de un modulo, ej.
/// General/Organizacional/Sistema de req001); Item cuelga de una Section o Subgroup; un Item nunca
/// tiene hijos. Extraida del servicio para poder testearla sin base de datos (Ola 2).
/// </summary>
public static class MenuNodeKindRules
{
    /// <summary>Mensaje de error si el Kind no es coherente con el padre, o null si es valido.</summary>
    public static string? Validate(MenuNodeKind kind, MenuNodeKind? parentKind) => kind switch
    {
        MenuNodeKind.QuickLink when parentKind is not null => "Un enlace rapido debe ir en el primer nivel (sin padre).",
        MenuNodeKind.Section when parentKind is not null => "Una seccion debe ir en el primer nivel (sin padre).",
        MenuNodeKind.Subgroup when parentKind is not (MenuNodeKind.Section or MenuNodeKind.Subgroup) => "Un subgrupo solo puede colgar de una seccion o de otro subgrupo.",
        MenuNodeKind.Item when parentKind is not (MenuNodeKind.Section or MenuNodeKind.Subgroup) => "Un elemento solo puede colgar de una seccion o subgrupo.",
        _ => null
    };
}
