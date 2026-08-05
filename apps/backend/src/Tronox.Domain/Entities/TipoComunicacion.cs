using Tronox.Domain.Common;
using Tronox.Domain.Enums;

namespace Tronox.Domain.Entities;

/// <summary>
/// Tipo de comunicacion radicable (RQ09 RF01-2). Trece tipos base normativos se precargan por tenant
/// (EsBase = true): no se eliminan, solo se editan o inactivan. Define reglas CPACA/PQRSD, terminos
/// legales (dias/tipo de dia/prorroga) y visibilidad en el portal ciudadano. Unico por (tenant, codigo).
/// TENANT-SCOPED.
/// </summary>
public class TipoComunicacion : TenantEntity
{
    /// <summary>Identificador unico sin espacios (ej. "PETICION").</summary>
    public string Codigo { get; set; } = null!;

    /// <summary>Nombre visible al usuario y al ciudadano (100).</summary>
    public string Nombre { get; set; } = null!;

    public RadicacionDireccion Direccion { get; set; } = RadicacionDireccion.Entrada;

    /// <summary>Activa reglas CPACA y reportes PQRSD.</summary>
    public bool EsPqrsd { get; set; }

    /// <summary>Bloquea prorroga y fuerza termino en dias calendario.</summary>
    public bool EsTutela { get; set; }

    /// <summary>Habilita el campo de acto administrativo.</summary>
    public bool EsRecurso { get; set; }

    /// <summary>Activa el control de terminos SLA.</summary>
    public bool RequiereRespuesta { get; set; }

    /// <summary>Dias de respuesta (si RequiereRespuesta).</summary>
    public int? DiasRespuesta { get; set; }

    public RadicacionTipoDia? TipoDia { get; set; }

    public RadicacionInicioTermino? InicioTermino { get; set; }

    /// <summary>Bloqueado en false si EsTutela.</summary>
    public bool Prorrogable { get; set; }

    /// <summary>Maximo igual al termino inicial.</summary>
    public int? DiasProrroga { get; set; }

    /// <summary>Si No, el remitente es obligatorio.</summary>
    public bool PermiteAnonimo { get; set; }

    /// <summary>Aparece en el portal ciudadano.</summary>
    public bool HabilitadoWeb { get; set; }

    /// <summary>Clasificacion por defecto (FK NivelClasificacion, RF06 de RQ02). NO ACTION.</summary>
    public long? NivelReservaDefaultId { get; set; }
    public NivelClasificacion? NivelReservaDefault { get; set; }

    /// <summary>Clave de icono (Bootstrap Icons) para diferenciacion visual.</summary>
    public string? Icono { get; set; }

    /// <summary>Color HEX de etiqueta en bandejas.</summary>
    public string? Color { get; set; }

    /// <summary>Palabras clave para deteccion automatica en correos.</summary>
    public string? PalabrasClave { get; set; }

    /// <summary>Orden en el portal ciudadano.</summary>
    public int? OrdenPortal { get; set; }

    /// <summary>Texto explicativo en el portal.</summary>
    public string? DescripcionCiudadano { get; set; }

    /// <summary>Inactivo oculta el tipo en todos los formularios.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Tipo base normativo precargado: no eliminable (solo editar/inactivar).</summary>
    public bool EsBase { get; set; }
}
