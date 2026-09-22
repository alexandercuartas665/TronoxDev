namespace Tronox.Domain.Enums;

/// <summary>
/// Subtipo estructural de un tercero del Catalogo (RQ07 RF01). Valor UNICO por tercero (no multivalor);
/// su seleccion define que campos dibuja la ficha. Los roles contextuales (Cliente, Proveedor, Ciudadano,
/// Contratista, Postulante...) NO son subtipos: son etiquetas multivalor (diferido). Persistido como string.
/// </summary>
public enum TerceroSubtipo
{
    PersonaNatural,
    JuridicaPrivada,
    JuridicaPublica,
    JuridicaMixta,
    ExtranjeraNatural,
    ExtranjeraJuridica
}
