using Ecorex.Application;
using Ecorex.Application.Common;
using Ecorex.Infrastructure;
using Ecorex.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddScoped<ITenantContext, SystemTenantContext>();

// Host de procesos asincronos de TRONOX. Los workers de las 17 specs (motor SLA de RQ09, cola
// de OCR, eventos de workflow de RQ11, notificaciones y ETL nocturno de RQ13) se registran aqui
// a medida que se construya cada modulo. Los del backbone no aplican a este dominio.

var host = builder.Build();
host.Run();
