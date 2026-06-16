using MarketingAutomation.Api;
using MarketingAutomation.Modules.Campaigns;
using MarketingAutomation.Modules.Campaigns.Endpoints;
using MarketingAutomation.Modules.Campaigns.Infrastructure;
using MarketingAutomation.Modules.Contacts;
using MarketingAutomation.Modules.Contacts.Endpoints;
using MarketingAutomation.Modules.Contacts.Infrastructure;
using MarketingAutomation.Modules.Events;
using MarketingAutomation.Modules.Events.Endpoints;
using MarketingAutomation.Modules.Events.Infrastructure;
using MarketingAutomation.Modules.Ai;
using MarketingAutomation.Modules.Journeys;
using MarketingAutomation.Modules.Journeys.Endpoints;
using MarketingAutomation.Modules.Journeys.Infrastructure;
using MarketingAutomation.Modules.Messaging;
using MarketingAutomation.Modules.Messaging.Endpoints;
using MarketingAutomation.Modules.Messaging.Infrastructure;
using MarketingAutomation.Modules.Platform;
using MarketingAutomation.Modules.Segments;
using MarketingAutomation.Modules.Segments.Endpoints;
using MarketingAutomation.Modules.Segments.Infrastructure;
using MarketingAutomation.Modules.Platform.Infrastructure;
using MarketingAutomation.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq(context.Configuration["Seq:Url"] ?? "http://localhost:5341"));

builder.Services.AddPlatformModule(builder.Configuration);
builder.Services.AddContactsModule(builder.Configuration);
builder.Services.AddEventsModule(builder.Configuration);
builder.Services.AddMessagingModule(builder.Configuration);
builder.Services.AddCampaignsModule(builder.Configuration);
builder.Services.AddSegmentsModule(builder.Configuration);
builder.Services.AddJourneysModule(builder.Configuration);
builder.Services.AddAiModule(builder.Configuration);
builder.Services.AddSharedApplication();

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<PlatformDbContext>("postgres");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<ContactsDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<EventsDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<MessagingDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<CampaignsDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<SegmentsDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<JourneysDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { service = "marketing-automation", status = "ok" }));
app.MapContactsEndpoints();
app.MapEventsEndpoints();
app.MapMessagingEndpoints();
app.MapCampaignsEndpoints();
app.MapSegmentsEndpoints();
app.MapJourneysEndpoints();

app.Run();

public partial class Program;
