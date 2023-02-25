﻿using Elasticsearch.Net;
using Logger_Microservice.Commands.LogCommands;
using Logger_Microservice.Commands.QueueCommands;
using Logger_Microservice.Handlers.LogHandlers;
using Logger_Microservice.Handlers.QueueHandlers;
using Logger_Microservice.ProjectConfigurations;
using Logger_Microservice.Queries.QueueQueries;
using Logger_Microservice.Repositories.Interfaces;
using Logger_Microservice.Repositories.Repositories;
using LoggerMicroservice.Models;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using RabbitMQ.Client;
using System.Reflection;
using IConnection = RabbitMQ.Client.IConnection;

var serviceProvider = new ServiceCollection();

EnvVariablesConfiguration envVariablesHandler = new EnvVariablesConfiguration();
ElkConfiguration elkEnvVariables = envVariablesHandler.GetElkEnvVariables();
RabbitMqConfiguration rabbitEnvVariables = envVariablesHandler.GetRabbitEnvVariables();

Console.WriteLine($"RABBITMQ_HOST {rabbitEnvVariables.RabbitMqHost}");
Console.WriteLine($"RABBITMQ_PORT {rabbitEnvVariables.RabbitMqPort}");
Console.WriteLine($"RABBITMQ_USERNAME {rabbitEnvVariables.RabbitMqUsername}");
Console.WriteLine($"RABBITMQ_PASSWORD {rabbitEnvVariables.RabbitMqPassword}");

Console.WriteLine($"ELK_HOST {elkEnvVariables.ElkHost}");
Console.WriteLine($"ELK_DEFAULT_INDEX {elkEnvVariables.ElkDefaultIndex}");
Console.WriteLine($"ELK_USERNAME {elkEnvVariables.ElkUsername}");
Console.WriteLine($"ELK_PASSWORD {elkEnvVariables.ElkPassword}");

ConnectionSettings connection = new ConnectionSettings(new Uri(elkEnvVariables.ElkHost)).
DefaultIndex(elkEnvVariables.ElkDefaultIndex).
ServerCertificateValidationCallback(CertificateValidations.AllowAll).
ThrowExceptions(true).
PrettyJson().
RequestTimeout(TimeSpan.FromSeconds(300)).
BasicAuthentication(elkEnvVariables.ElkUsername, elkEnvVariables.ElkPassword); //.ApiKeyAuthentication("<id>", "<api key>"); 
ElasticClient elasticClient = new ElasticClient(connection);
serviceProvider.AddSingleton<IElasticClient>(elasticClient);

var connectionFactory = new ConnectionFactory
{
    HostName = rabbitEnvVariables.RabbitMqHost,
    Port = Convert.ToInt32(rabbitEnvVariables.RabbitMqPort),
    UserName = rabbitEnvVariables.RabbitMqUsername,
    Password = rabbitEnvVariables.RabbitMqPassword
};
IConnection rabbitConnection = connectionFactory.CreateConnection();
serviceProvider.AddSingleton(rabbitConnection);

//Repository
serviceProvider.AddScoped(typeof(IQueueRepository<>), typeof(QueueRepository<>));
serviceProvider.AddScoped(typeof(ILogRepository<>), typeof(LogRepository<>));
serviceProvider.AddScoped<ILog4NetRepository, Log4NetRepository>();

Assembly.GetAssembly(typeof(LogHandler<>));
Assembly.GetAssembly(typeof(QueueErrorQueryHandler<>));
Assembly.GetAssembly(typeof(QueueCommandHandler<>));

Assembly.GetAssembly(typeof(LogCommand<>));
Assembly.GetAssembly(typeof(QueueCommand<>));

var Handlers = AppDomain.CurrentDomain.Load("Logger-Microservice.Handlers");
var Queries = AppDomain.CurrentDomain.Load("Logger-Microservice.Queries");
var Commands = AppDomain.CurrentDomain.Load("Logger-Microservice.Commands");

serviceProvider.AddMediatR(Handlers);
serviceProvider.AddMediatR(Queries);
serviceProvider.AddMediatR(Commands);

serviceProvider.AddLazyResolution();
var builder = serviceProvider.BuildServiceProvider();

IMediator _mediator = builder.GetService<IMediator>();


try
{
    await Task.Run(() =>
    {
        _mediator.Send(new QueueQuery("errorlogs")).GetAwaiter();
    });

    await Task.Run(() =>
    {
        _mediator.Send(new QueueQuery("otherlogs")).GetAwaiter();
    });

}
catch (Exception exception)
{
    QueueLog queueLog = new QueueLog()
    {
        OperationType = "Program.cs",
        Date = DateTime.Now,
        ExceptionMessage = exception.Message.ToString()
    };
    ErrorLog errorLog = new ErrorLog()
    {
        queueLog = queueLog
    };
    _mediator.Send(new QueueCommand<ErrorLog>(errorLog, "errorlogs", "log_exchange.direct", "error_log")).GetAwaiter();

}