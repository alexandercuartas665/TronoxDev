namespace Tronox.Web.Components.Shared;

/// <summary>
/// Modo de operacion del <c>DynamicFormRenderer</c> (RQ08, port ECOREX): vista previa del diseñador,
/// llenado (captura + envio), solo lectura de un registro, o vista embebida.
/// </summary>
public enum FormRenderMode
{
    /// <summary>Vista previa en el diseñador (no persiste).</summary>
    Design,
    /// <summary>Llenado: captura, autosave y envio con validacion servidor.</summary>
    Fill,
    /// <summary>Solo lectura de una respuesta/registro.</summary>
    ReadOnly,
    /// <summary>Vista embebida de una respuesta.</summary>
    View
}
