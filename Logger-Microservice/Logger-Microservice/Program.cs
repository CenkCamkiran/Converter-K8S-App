﻿using Converter_Microservice.Common.Events;
using Elasticsearch.Net;
using Logger_Microservice.Commands.LogCommands;
using Logger_Microservice.Commands.QueueCommands;
using Logger_Microservice.Handlers.LogHandlers;
using Logger_Microservice.Handlers.QueueHandlers;
using Logger_Microservice.Queries.QueueQueries;
using Logger_Microservice.Repositories.Interfaces;
using Logger_Microservice.Repositories.Providers;
using Logger_Microservice.Repositories.Repositories;
using LoggerMicroservice.Models;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using RabbitMQ.Client;
using WebService.Common.Constants;
using IConnection = RabbitMQ.Client.IConnection;

var serviceProvider = new ServiceCollection();


//ELK
ConnectionSettings connection = new ConnectionSettings(new Uri(ProjectConstants.ElkHost)).
DefaultIndex(ProjectConstants.ElkDefaultIndexName).
ServerCertificateValidationCallback(CertificateValidations.AllowAll).
ThrowExceptions(ProjectConstants.ElkExceptions).
PrettyJson().
RequestTimeout(TimeSpan.FromSeconds(ProjectConstants.ElkRequestTimeout)).
BasicAuthentication(ProjectConstants.ElkUsername, ProjectConstants.ElkPassword);
ElasticClient elasticClient = new ElasticClient(connection);
serviceProvider.AddSingleton<IElasticClient>(elasticClient);


//RabbitMQ
var connectionFactory = new ConnectionFactory
{
    HostName = ProjectConstants.RabbitmqHost,
    Port = Convert.ToInt32(ProjectConstants.RabbitmqPort),
    UserName = ProjectConstants.RabbitmqUsername,
    Password = ProjectConstants.RabbitmqPassword
};
IConnection rabbitConnection = connectionFactory.CreateConnection();
serviceProvider.AddSingleton(rabbitConnection);


//Repository
serviceProvider.AddScoped(typeof(IQueueRepository), typeof(QueueRepository));
serviceProvider.AddScoped(typeof(ILogRepository), typeof(LogRepository));
serviceProvider.AddScoped<ILog4NetRepository, Log4NetRepository>();


serviceProvider.AddMediatR((MediatRServiceConfiguration configuration) =>
{
    configuration.RegisterServicesFromAssemblies(
        typeof(LogCommand).Assembly,
        typeof(QueueCommand).Assembly,
        typeof(LogHandler).Assembly,
        typeof(QueueErrorQueryHandler).Assembly,
        typeof(QueueCommandHandler).Assembly,
        typeof(QueueErrorQueryHandler).Assembly,
        typeof(QueueErrorQuery).Assembly,
        typeof(QueueOtherQuery).Assembly
        );
});

serviceProvider.AddLazyResolution();
var builder = serviceProvider.BuildServiceProvider();

IMediator _mediator = builder.GetService<IMediator>();
ILog4NetRepository _log4NetRepository = builder.GetService<ILog4NetRepository>();

CancellationTokenSource cts = new CancellationTokenSource();
CancellationToken ct = cts.Token;

try
{
    var errorLogsTask = _mediator.Send(new QueueErrorQuery(ProjectConstants.ErrorLogsServiceQueueName), ct);
    var otherLogsTask = _mediator.Send(new QueueOtherQuery(ProjectConstants.OtherLogsServiceQueueName), ct);

    await Task.WhenAll(errorLogsTask, otherLogsTask);

}
catch (Exception exception)
{
    QueueLog queueLog = new QueueLog()
    {
        OperationType = LogEvents.ConsumeLogsEvent,
        Date = DateTime.Now,
        ExceptionMessage = exception.Message.ToString()
    };
    ErrorLog errorLog = new ErrorLog()
    {
        queueLog = queueLog
    };
    _log4NetRepository.Error(exception.Message.ToString());

    await _mediator.Send(new QueueCommand(errorLog, ProjectConstants.ErrorLogsServiceQueueName, ProjectConstants.ErrorLogsServiceExchangeName, ProjectConstants.ErrorLogsServiceRoutingKey));

}
