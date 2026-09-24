using PersonalTechBrief.Application;
using PersonalTechBrief.Infrastructure;
using PersonalTechBrief.Infrastructure.Analysis;
using PersonalTechBrief.Infrastructure.Intelligence;
using PersonalTechBrief.Infrastructure.Messaging;
using PersonalTechBrief.Processor;
using PersonalTechBrief.Processor.Messaging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMessagingInfrastructure(builder.Configuration);
builder.Services.AddIntelligenceApiClient(builder.Configuration);
builder.Services.AddGroupingAndRelevance(builder.Configuration);
builder.Services.AddScoped<SourceItemReadyMessageHandler>();
builder.Services.AddHostedService<OutboxDispatcherWorker>();
builder.Services.AddHostedService<SourceItemReadyProcessorWorker>();
builder.Services.AddHostedService<GroupingConsumerWorker>();

var host = builder.Build();
host.Run();
