namespace Tronox.Application.MenuConfig;

/// <summary>
/// Aprovisionamiento del MENU de un tenant.
///
/// Bug que corrige: el menu solo se sembraba para el tenant DEMO (seeder), asi que un cliente creado
/// por el alta (PlatformAdmin) o por el onboarding nacia SIN NINGUNA vista de menu y sus usuarios no
/// veian nada. Ahora ambos caminos de alta siembran la vista "Completo" (por defecto, arbol canonico).
///
/// La implementacion vive en Infrastructure (DatabaseSeeder), que es la duena del arbol canonico:
/// asi el demo y el alta comparten la MISMA definicion y no derivan.
/// </summary>
public interface IMenuProvisioningService
{
    /// <summary>
    /// Actualiza el ROTULO de los nodos del tenant que aun conservan un nombre de una version
    /// anterior del catalogo canonico. Solo toca los que coinciden EXACTAMENTE con el nombre viejo:
    /// si el tenant lo renombro en el editor de vistas, se respeta. Devuelve cuantos renombro.
    /// </summary>
    Task<int> BackfillCanonicalNamesAsync(long tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Siembra la vista "Completo" (IsDefault) con el arbol canonico si el tenant aun no tiene
    /// NINGUNA vista de menu. Idempotente: si ya tiene vistas, no hace nada.
    /// </summary>
    Task EnsureDefaultMenuAsync(long tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rellena la clave de icono de los nodos del tenant que la tengan vacia, emparejando por ruta
    /// contra el arbol canonico. NO pisa el icono que el tenant haya elegido: solo llena huecos.
    /// Existe para los tenants creados antes de que los items del catalogo llevaran icono.
    /// Devuelve cuantos nodos se rellenaron.
    /// </summary>
    Task<int> BackfillIconKeysAsync(long tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-alinea la vista predeterminada de un tenant EXISTENTE con el arbol canonico vigente cuando
    /// esa vista sigue INTACTA (el tenant no la personalizo en el editor). Necesario porque el arbol
    /// cambio de estructura al alinearse con el prototipo (nodos nuevos, movidos, con rutas distintas):
    /// el aprovisionamiento idempotente no re-siembra un tenant que ya tiene items, asi que sin este
    /// paso los tenants creados con la version anterior conservarian el arbol viejo.
    ///
    /// SEGURO: solo re-siembra si la vista es PRISTINA (un unico perfil, sin usuarios con vista
    /// asignada, todos los nodos con nombre/icono/visibilidad canonicos y sin rutas ajenas al
    /// catalogo). Si el tenant toco cualquier cosa, no se reconstruye nada. Devuelve true si
    /// re-sembro. La matriz de permisos la vuelve a rellenar el aprovisionamiento de roles.
    /// </summary>
    Task<bool> ReconciliarVistaPredeterminadaAsync(long tenantId, CancellationToken cancellationToken = default);
}
