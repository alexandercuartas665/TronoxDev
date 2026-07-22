using System.Globalization;
using System.Security.Claims;
using Ecorex.Application;
using Ecorex.Application.Common;
using Ecorex.Application.Common.Auth;
using Ecorex.Domain.Enums;
using Ecorex.Infrastructure;
using Ecorex.Infrastructure.Persistence;
using Ecorex.SuperAdmin.Auth;
using Ecorex.SuperAdmin.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Override local NO versionado (gitignored: appsettings.*.local.json) para apuntar el dev a una
// BD propia (p.ej. la de la nube dev/staging) sin poner la credencial en el repo publico. Si el
// archivo no existe, no pasa nada; la conexion sigue saliendo de ECOREX_DB_CONNECTION / appsettings.
builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: false);

// Formato numerico uniforme en todo el sistema, independiente del locale del servidor (dev o Railway):
// coma = separador de miles, punto = decimal (ej. 3,500,000.50). Evita que el host cambie como se ven los montos.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    // Sube el limite de mensajes del circuito SignalR: al arrastrar y soltar archivos al chat,
    // el contenido viaja como base64 por invokeMethodAsync y el limite por defecto (32 KB) lo
    // rechazaba en silencio. 32 MB cubre el tope de 16 MB del archivo (~21 MB en base64).
    .AddHubOptions(options => options.MaximumReceiveMessageSize = 32L * 1024 * 1024);

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        // 30 dias deslizantes: con "Recordar sesion" marcado la cookie es persistente (IsPersistent)
        // y dura hasta 30 dias renovandose en cada visita; sin marcar es cookie de sesion (muere al
        // cerrar el navegador). Antes eran 8h, lo que obligaba a re-loguear con frecuencia.
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorizationBuilder()
    // Operador de plataforma (Super Admin / roles internos): tiene claim platform_role.
    .AddPolicy("PlatformOperator", p => p.RequireClaim("platform_role"))
    // Solo SuperAdmin (alta del equipo de plataforma).
    .AddPolicy("SuperAdminOnly", p => p.RequireClaim("platform_role", "SuperAdmin"))
    // Miembro de una agencia: tiene claim tenant_id.
    .AddPolicy("TenantMember", p => p.RequireClaim("tenant_id"))
    // ---- Policies por modulo ----
    // Ola 7 (endurecimiento) PASO 2 REALIZADO para la familia Tareas: estas policies ya no son
    // placeholder de tenant_id: derivan el requisito REAL del Module Registry (PermissionRequirement
    // Modulo x Accion), sin tocar las paginas -solo cambia la definicion aqui-. El handler concede a
    // Owner/Admin y sin-rol (Unrestricted, fail-open); solo un rol limitado sin el permiso queda fuera.
    // "Formularios.Disenar" es una POLICY COMPUESTA (multi-permiso del legacy): exige VER *y* EDITAR
    // formularios (dos PermissionRequirement = AND). Route-keys del catalogo: actividades/proyectos/
    // flujos/formularios (ModuleCatalogFallback / menu Item Route).
    .AddPolicy("Tareas.Ver", p => p.RequireClaim("tenant_id")
        .AddRequirements(new Ecorex.SuperAdmin.Auth.PermissionRequirement("actividades", Ecorex.Application.Roles.PermissionAction.View)))
    .AddPolicy("Proyectos.Ver", p => p.RequireClaim("tenant_id")
        .AddRequirements(new Ecorex.SuperAdmin.Auth.PermissionRequirement("proyectos", Ecorex.Application.Roles.PermissionAction.View)))
    .AddPolicy("Flujos.Ver", p => p.RequireClaim("tenant_id")
        .AddRequirements(new Ecorex.SuperAdmin.Auth.PermissionRequirement("flujos", Ecorex.Application.Roles.PermissionAction.View)))
    // ADR-0038: la policy "MisPasos.Ver" y la pagina /mis-pasos fueron RETIRADAS. El runtime de
    // flujos vive en el detalle de la tarea; los pasos pendientes se descubren en el tablero.
    // COMPUESTA (AND): disenar formularios exige ver Y editar el modulo formularios.
    .AddPolicy("Formularios.Disenar", p => p.RequireClaim("tenant_id")
        .AddRequirements(new Ecorex.SuperAdmin.Auth.PermissionRequirement("formularios", Ecorex.Application.Roles.PermissionAction.View))
        .AddRequirements(new Ecorex.SuperAdmin.Auth.PermissionRequirement("formularios", Ecorex.Application.Roles.PermissionAction.Edit)))
    .AddPolicy("Reglas.Editar", p => p.RequireClaim("tenant_id"))
    .AddPolicy("Conceptos.Editar", p => p.RequireClaim("tenant_id"))
    .AddPolicy("Dependencias.Ver", p => p.RequireClaim("tenant_id"))
    .AddPolicy("ModulosWeb.Administrar", p => p.RequireClaim("tenant_id"))
    .AddPolicy("ExtraccionDatos.Editar", p => p.RequireClaim("tenant_id"))
    // Inventarios (grupo Sistema - Inventarios, ADR-0027): items 000066 + catalogos
    // 000556/000502/000506/000606/000498. Paso 1: mismo requisito que TenantMember.
    .AddPolicy("Inventario.Ver", p => p.RequireClaim("tenant_id"))
    // Plantillas HSM de WhatsApp (ADR-0029): editor de plantillas del CRM heredado. Paso 1:
    // mismo requisito que TenantMember (claim tenant_id). Paso 2 (Module Registry) pendiente.
    .AddPolicy("PlantillasWhatsApp.Editar", p => p.RequireClaim("tenant_id"))
    // Administrador de Menu (menu configurable por perfil, Ola 2, ADR-0030): editor de vistas
    // y nodos del menu del workspace. Paso 1: mismo requisito que TenantMember (claim tenant_id)
    // para no cambiar el acceso. TODO (paso 2): restringir a Owner/Admin del tenant (tenant_role)
    // -es una pantalla de gobierno del tenant- derivandolo del rol del TenantUser.
    // Ola 7 (endurecimiento) - policy de GOBIERNO: el editor del menu del tenant es una pantalla de
    // gobierno, se restringe a Owner/Admin del tenant (claim tenant_role), no a cualquier miembro.
    .AddPolicy("ConfiguracionMenu.Administrar", p => p.RequireClaim("tenant_role", "Owner", "Admin"))
    // Administracion de usuarios del tenant (modulo 000073, ADR-0031): CRUD de usuarios del
    // tenant (invitar, rol, estado, clave, vista de menu). Paso 1: mismo requisito que
    // TenantMember (claim tenant_id) para no cambiar el acceso. TODO (paso 2): restringir a
    // Owner/Admin del tenant (claim tenant_role) -es gobierno del tenant- derivandolo del rol.
    .AddPolicy("AdmUsuarios.Editar", p => p.RequireClaim("tenant_id"))
    // Roles y permisos (Ola B1, ADR-0032): matriz de permisos Modulo x Accion por tenant y
    // asignacion de rol a usuario. Paso 1: mismo requisito que TenantMember (claim tenant_id)
    // para no cambiar el acceso. TODO (paso 2 / Ola B2): restringir a Owner/Admin del tenant
    // (claim tenant_role) -es gobierno del tenant- y hacer cumplir el set efectivo por modulo.
    .AddPolicy("RolesPermisos.Administrar", p => p.RequireClaim("tenant_id"))
    // Ficha de empresa / administracion de tenants (modulo 000072, ADR-0026). Es GOBIERNO
    // multi-tenant: vive en el area PlatformAdmin junto a /tenants y /plans, por eso exige
    // platform_role (igual que PlatformOperator), NO tenant_id. El item 000072 del NavMenu
    // se movio del menu del tenant al area de plataforma.
    .AddPolicy("AdmEmpresas.Ver", p => p.RequireClaim("platform_role"));

// Enforcement dinamico de permisos por rol (Ola B2, ADR-0033). El policy provider materializa al
// vuelo las policies con prefijo "Perm:{moduleKey}:{action}" (gate tenant_id + PermissionRequirement)
// y DELEGA el resto en el default provider, asi que las policies existentes no cambian. El handler
// consulta ICurrentPermissions (regla opt-in: Owner/Admin y sin-rol = Unrestricted; fail-open).
builder.Services.AddScoped<Ecorex.SuperAdmin.Auth.ICurrentPermissions, Ecorex.SuperAdmin.Auth.CurrentPermissions>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, Ecorex.SuperAdmin.Auth.PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Ecorex.SuperAdmin.Auth.PermissionAuthorizationHandler>();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
if (builder.Environment.IsDevelopment())
{
    // DataProtection en DEV: persiste las llaves a un archivo LOCAL en vez del DbContext. Dos razones:
    // (1) los reinicios del dev ya no cierran la sesion (llaves estables entre arranques); (2) el dev
    // deja de escribir su keyring en la BD, que en este proyecto es la de PROD via tunel. Solo Development;
    // en produccion queda la persistencia al DbContext que registra Infrastructure. La carpeta esta gitignored.
    builder.Services.AddDataProtection()
        .SetApplicationName("Ecorex")
        .PersistKeysToFileSystem(new System.IO.DirectoryInfo(
            System.IO.Path.Combine(builder.Environment.ContentRootPath, ".dpkeys-dev")));
}
// Contexto de tenant con soporte ambient: por cookie en peticiones, fijable en background (webhook -> agente).
builder.Services.AddScoped<ITenantContext, Ecorex.SuperAdmin.Auth.AmbientTenantContext>();

builder.Services.AddSignalR();
builder.Services.AddScoped<Ecorex.Application.Notifications.INotificationBroadcaster, Ecorex.SuperAdmin.RealTime.SignalRNotificationBroadcaster>();
builder.Services.AddScoped<Ecorex.SuperAdmin.Services.CircuitFormGate>();

var app = builder.Build();

// Detras del proxy de Railway (TLS en el borde, HTTP al contenedor): leer
// X-Forwarded-Proto/For para que Request.Scheme sea "https". Asi las cookies
// seguras del login y UseHttpsRedirection funcionan sin bucles de redireccion.
// Debe ir lo antes posible en el pipeline.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedOptions.KnownIPNetworks.Clear(); // KnownNetworks quedo obsoleto en .NET 10 (ASPDEPR005)
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();

    // En produccion las migraciones NO se aplican solas. Si ECOREX_RUN_MIGRATIONS=true
    // (variable de Railway), aplicar las migraciones pendientes al arrancar. Es seguro
    // con una sola instancia web; el seed de demo no corre en produccion.
    if (string.Equals(Environment.GetEnvironmentVariable("ECOREX_RUN_MIGRATIONS"), "true", StringComparison.OrdinalIgnoreCase))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EcorexDbContext>();
        await db.Database.MigrateAsync();
    }
}

app.UseHttpsRedirection();
// Sirve archivos subidos en tiempo de ejecucion (logos de agencias en wwwroot/uploads).
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<Ecorex.SuperAdmin.RealTime.NotificationHub>("/hubs/notifications");

app.MapPost("/auth/login", async (
    HttpContext http,
    [FromForm] string email,
    [FromForm] string password,
    [FromForm] string? remember,
    IApplicationDbContext db,
    IPasswordHasher hasher) =>
{
    var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
    var user = await db.PlatformUsers.FirstOrDefaultAsync(u => u.Email == normalized);

    if (user is null
        || string.IsNullOrEmpty(user.PasswordHash)
        || !hasher.Verify(user.PasswordHash, password ?? string.Empty))
    {
        return Results.Redirect("/login?error=1");
    }
    // Si la clave es correcta pero la cuenta esta pendiente de activacion, redirige al flujo de activacion.
    if (user.Status == PlatformUserStatus.PendingActivation)
    {
        return Results.Redirect($"/activar?email={Uri.EscapeDataString(normalized)}&error={Uri.EscapeDataString("Activa tu cuenta antes de iniciar sesion. Te enviamos un codigo a tu correo.")}");
    }
    if (user.Status != PlatformUserStatus.Active)
    {
        return Results.Redirect("/login?error=1");
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.DisplayName ?? user.Email),
        new(ClaimTypes.Email, user.Email)
    };

    string redirect;
    var isOperator = user.PlatformRole is PlatformRole platformRole;
    if (isOperator)
    {
        claims.Add(new Claim("platform_role", user.PlatformRole!.Value.ToString()));
    }

    // Membresia de agencia: la resolvemos para TODOS los usuarios (operador o no). Un operador
    // de plataforma que ademas sea miembro de un tenant (ej. el Super Admin como Owner del tenant
    // interno "Plataforma ECOREX") recibe los dos claims y puede usar tanto la consola de gobierno
    // como los modulos comerciales tenant-scoped (Pipeline, Conversaciones, etc.).
    var membership = await db.TenantUsers
        .IgnoreQueryFilters()
        .Where(tu => tu.PlatformUserId == user.Id && tu.Status == PlatformUserStatus.Active)
        .OrderBy(tu => tu.CreatedAt)
        .FirstOrDefaultAsync();

    if (!isOperator && membership is null)
    {
        // Identidad valida pero sin rol de plataforma ni membresia activa: sin acceso.
        return Results.Redirect("/login?error=1");
    }

    if (membership is not null)
    {
        claims.Add(new Claim("tenant_id", membership.TenantId.ToString()));
        claims.Add(new Claim("tenant_role", membership.TenantRole.ToString()));
    }

    // Operador de plataforma -> Dashboard; usuario de tenant -> Inicio del workspace (prototipo final).
    redirect = isOperator ? "/" : "/inicio";

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    // "Recordar sesion": checkbox marcado -> cookie PERSISTENTE (sobrevive al cierre del navegador,
    // 30 dias deslizantes). Sin marcar -> cookie de sesion (muere al cerrar), con la expiracion de 8h.
    var rememberMe = string.Equals(remember, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(remember, "on", StringComparison.OrdinalIgnoreCase);
    var authProps = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
    {
        IsPersistent = rememberMe,
        ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(30) : null
    };
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), authProps);
    return Results.Redirect(redirect);
}).DisableAntiforgery();

// Auto-registro (autogestion): un visitante crea su propia agencia + usuario Owner. La cuenta
// queda en PendingActivation; se envia un codigo de 6 digitos por correo y el visitante debe
// ingresarlo en /activar antes de poder iniciar sesion. La agencia nace activa sin plan.
app.MapPost("/auth/register", async (
    HttpContext http,
    [FromForm] string agencyName,
    [FromForm] string displayName,
    [FromForm] string email,
    [FromForm] string password,
    Ecorex.Application.Auth.ISelfSignupService signup) =>
{
    var result = await signup.SignUpAsync(
        new Ecorex.Application.Auth.SelfSignupRequest(agencyName, displayName, email, password));

    if (!result.Success)
    {
        var msg = Uri.EscapeDataString(result.Error ?? "No se pudo crear la cuenta.");
        return Results.Redirect($"/login?mode=signup&regerror={msg}");
    }

    // No iniciamos sesion: el usuario debe activar la cuenta con el codigo enviado por correo.
    // Si la cuenta se creo pero el envio del correo fallo (SMTP mal configurado, etc.), llevamos
    // al visitante a /activar con un aviso en lugar de a /login con un error opaco. Alli puede
    // usar "Reenviar codigo" cuando el correo este disponible.
    var qs = $"email={Uri.EscapeDataString(result.Email)}";
    if (!string.IsNullOrWhiteSpace(result.EmailDeliveryWarning))
    {
        qs += $"&error={Uri.EscapeDataString(result.EmailDeliveryWarning)}";
    }
    else
    {
        qs += "&sent=1";
    }
    return Results.Redirect($"/activar?{qs}");
}).DisableAntiforgery();

// Activa la cuenta del visitante usando el codigo recibido por correo. Si es valido, inicia
// la sesion automaticamente y redirige a "Mi cuenta".
app.MapPost("/auth/activate", async (
    HttpContext http,
    [FromForm] string email,
    [FromForm] string code,
    Ecorex.Application.Auth.IAccountActivationService activation) =>
{
    var result = await activation.ActivateAsync(email, code);
    if (!result.Ok || result.PlatformUserId is null)
    {
        var msg = Uri.EscapeDataString(result.Error ?? "Codigo invalido o expirado.");
        return Results.Redirect($"/activar?email={Uri.EscapeDataString(email ?? string.Empty)}&error={msg}");
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, result.PlatformUserId.Value.ToString()),
        new(ClaimTypes.Name, result.Email ?? string.Empty),
        new(ClaimTypes.Email, result.Email ?? string.Empty)
    };
    if (result.TenantId is { } tid)
    {
        claims.Add(new Claim("tenant_id", tid.ToString()));
        claims.Add(new Claim("tenant_role", TenantRole.Owner.ToString()));
    }

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Redirect("/mi-cuenta");
}).DisableAntiforgery();

// Reenvio del codigo de activacion: invalida los codigos previos y emite uno nuevo. La respuesta
// siempre es uniforme para no revelar si el correo existe o ya esta activado.
app.MapPost("/auth/resend-activation", async (
    [FromForm] string email,
    Ecorex.Application.Auth.IAccountActivationService activation,
    Ecorex.Application.Common.IEmailSender emailSender,
    Ecorex.Application.Admin.IPlatformBrandingService branding) =>
{
    var result = await activation.ResendAsync(email);
    if (result.Ok && !string.IsNullOrEmpty(result.Code))
    {
        var brand = await branding.GetAsync();
        var html = $@"<div style=""font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:0 auto;color:#1f2937;"">
  <h2 style=""color:#4f46e5;"">{brand.PlatformName}</h2>
  <p>Aqui esta tu nuevo codigo de activacion:</p>
  <p style=""text-align:center;margin:28px 0;"">
    <span style=""display:inline-block;background:#eef2ff;color:#1e1b4b;font-size:26px;letter-spacing:6px;font-weight:bold;padding:14px 24px;border-radius:10px;border:1px solid #c7d2fe;"">{result.Code}</span>
  </p>
  <p>Este codigo vence en 24 horas y solo puede usarse una vez.</p>
</div>";
        await emailSender.SendAsync(email, $"Tu codigo de activacion - {brand.PlatformName}", html);
    }
    return Results.Redirect($"/activar?email={Uri.EscapeDataString(email ?? string.Empty)}&sent=1");
}).DisableAntiforgery();

// Recuperar contrasena (autogestion): envia un enlace de reseteo por correo. Nunca revela si el
// correo existe. El enlace usa el host de la peticion (sirve en dev y en prod tras forwarded headers).
app.MapPost("/auth/forgot", async (
    HttpContext http,
    [FromForm] string email,
    Ecorex.Application.Auth.IPasswordResetService reset) =>
{
    var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
    var result = await reset.RequestAsync(email, baseUrl);
    if (!result.Success)
    {
        return Results.Redirect($"/recuperar?error={Uri.EscapeDataString(result.Error ?? "No se pudo procesar la solicitud.")}");
    }
    return Results.Redirect("/recuperar?sent=1");
}).DisableAntiforgery();

// Aplica la nueva contrasena usando el token del enlace del correo.
app.MapPost("/auth/reset", async (
    [FromForm] string token,
    [FromForm] string password,
    Ecorex.Application.Auth.IPasswordResetService reset) =>
{
    var result = await reset.ResetAsync(token, password);
    if (!result.Success)
    {
        return Results.Redirect($"/restablecer?token={Uri.EscapeDataString(token)}&error={Uri.EscapeDataString(result.Error ?? "No se pudo restablecer la contrasena.")}");
    }
    return Results.Redirect("/login?reset=1");
}).DisableAntiforgery();

// Inicia el flujo OIDC con Google: arma la URL de challenge y guarda un state (proteccion CSRF).
// Con mode=signup se recuerda el nombre de la agencia para crear el tenant al volver del callback.
app.MapGet("/connect/google", async (
    HttpContext http,
    [FromQuery] string? mode,
    [FromQuery] string? agency,
    Ecorex.Application.Auth.IGoogleSignInService google) =>
{
    var redirectUri = $"{http.Request.Scheme}://{http.Request.Host}/signin-google";
    var state = Guid.NewGuid().ToString("N");
    var url = await google.BuildAuthorizeUrlAsync(redirectUri, state);
    if (url is null) { return Results.Redirect("/login?gerror=" + Uri.EscapeDataString("El ingreso con Google no esta habilitado.")); }

    var cookieOpts = new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = http.Request.IsHttps,
        MaxAge = TimeSpan.FromMinutes(10),
        Path = "/"
    };
    http.Response.Cookies.Append("g_oauth_state", state, cookieOpts);

    var isSignup = string.Equals(mode, "signup", StringComparison.OrdinalIgnoreCase);
    if (isSignup && !string.IsNullOrWhiteSpace(agency))
    {
        http.Response.Cookies.Append("g_signup_agency", Uri.EscapeDataString(agency.Trim()), cookieOpts);
    }
    else
    {
        http.Response.Cookies.Delete("g_signup_agency");
    }
    return Results.Redirect(url);
}).AllowAnonymous();

// Callback de Google: valida el state, intercambia el code y, si el usuario existe y esta activo,
// inicia sesion por cookie. No hay auto-registro: usuarios desconocidos reciben un mensaje claro.
app.MapGet("/signin-google", async (
    HttpContext http,
    [FromQuery] string? code,
    [FromQuery] string? state,
    [FromQuery] string? error,
    Ecorex.Application.Auth.IGoogleSignInService google,
    EcorexDbContext db) =>
{
    if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
    {
        return Results.Redirect("/login?gerror=" + Uri.EscapeDataString("No se completo el ingreso con Google."));
    }

    var expectedState = http.Request.Cookies["g_oauth_state"];
    http.Response.Cookies.Delete("g_oauth_state");

    var signupAgencyRaw = http.Request.Cookies["g_signup_agency"];
    http.Response.Cookies.Delete("g_signup_agency");
    var signupAgency = string.IsNullOrWhiteSpace(signupAgencyRaw) ? null : Uri.UnescapeDataString(signupAgencyRaw);

    if (string.IsNullOrEmpty(state) || !string.Equals(state, expectedState, StringComparison.Ordinal))
    {
        return Results.Redirect("/login?gerror=" + Uri.EscapeDataString("Sesion de ingreso invalida. Intenta de nuevo."));
    }

    var redirectUri = $"{http.Request.Scheme}://{http.Request.Host}/signin-google";
    var result = await google.ResolveAsync(code, redirectUri, signupAgency);
    if (!result.Success)
    {
        // Si venia del formulario de registro, mostramos el error dentro del panel "Crear cuenta".
        if (signupAgency is not null)
        {
            return Results.Redirect("/login?mode=signup&regerror=" + Uri.EscapeDataString(result.Error ?? "No se pudo crear la cuenta con Google."));
        }
        return Results.Redirect("/login?gerror=" + Uri.EscapeDataString(result.Error ?? "No se pudo iniciar sesion con Google."));
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
        new(ClaimTypes.Name, result.DisplayName ?? result.Email ?? string.Empty),
        new(ClaimTypes.Email, result.Email ?? string.Empty)
    };

    string redirect;
    var isOperator = result.PlatformRole is not null;
    if (isOperator)
    {
        claims.Add(new Claim("platform_role", result.PlatformRole!));
    }

    // Si el resultado de Google ya trae tenant_id (login de tenant), lo usamos; si no, miramos si
    // el usuario es miembro de algun tenant (caso Super Admin con tenant interno "Plataforma ECOREX").
    if (result.TenantId is { } resultTenantId)
    {
        claims.Add(new Claim("tenant_id", resultTenantId.ToString()));
        claims.Add(new Claim("tenant_role", result.TenantRole ?? TenantRole.Owner.ToString()));
    }
    else if (isOperator)
    {
        var membership = await db.TenantUsers
            .IgnoreQueryFilters()
            .Where(tu => tu.PlatformUserId == result.UserId && tu.Status == PlatformUserStatus.Active)
            .OrderBy(tu => tu.CreatedAt)
            .FirstOrDefaultAsync();
        if (membership is not null)
        {
            claims.Add(new Claim("tenant_id", membership.TenantId.ToString()));
            claims.Add(new Claim("tenant_role", membership.TenantRole.ToString()));
        }
    }

    // Operador de plataforma -> Dashboard; usuario de tenant -> Inicio del workspace (prototipo final).
    redirect = isOperator ? "/" : "/inicio";

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return Results.Redirect(redirect);
}).AllowAnonymous();

app.MapPost("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).DisableAntiforgery();

// Endpoint DEMO del modulo de extraccion de datos (000730, ADR-0025): JSON estatico de
// items para que la fuente demo y los tests E2E corran sin depender de internet. Es
// AllowAnonymous a proposito: el ejecutor hace un GET sin cookies (datos ficticios, sin
// informacion de tenant). El guard SSRF solo lo alcanza via loopback en Development.
app.MapGet("/comprobante/{paymentId:guid}", async (
    Guid paymentId,
    HttpContext http,
    Ecorex.Application.Admin.IPaymentReceiptService receipts) =>
{
    var receipt = await receipts.GenerateAsync(paymentId);
    if (receipt is null)
    {
        return Results.NotFound();
    }

    var isOperator = http.User.FindFirst("platform_role") is not null;
    var ownsTenant = Guid.TryParse(http.User.FindFirst("tenant_id")?.Value, out var tid) && tid == receipt.TenantId;
    if (!isOperator && !ownsTenant)
    {
        return Results.Forbid();
    }

    return Results.File(receipt.Content, "application/pdf", receipt.FileName);
}).RequireAuthorization();

// Webhook crudo de Evolution: traduce el evento, deduce el tenant del nombre de instancia,
// valida un token global y persiste el entrante (con difusion SignalR en este mismo proceso).
app.Run();

namespace Ecorex.SuperAdmin
{
    /// <summary>Cuerpo del emulador de canal: texto del cliente + opciones de prueba + imagen opcional (base64).</summary>
    public sealed record TestAgentRequest(string? Text = null, Guid? AgentId = null, string? ContactPhone = null, string? ContactName = null, string? ImageBase64 = null, string? ImageMime = null);
}
