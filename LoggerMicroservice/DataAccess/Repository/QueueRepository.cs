﻿using Configuration;
using DataAccess.Interfaces;
using log4net;
using Models;
using Nest;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DataAccess.Repository
{
    public class QueueRepository<TMessage> : IQueueRepository<TMessage> where TMessage : class
    {
        private List<QueueMessage> messageList = new List<QueueMessage>();
        private Log4NetRepository log = new Log4NetRepository();
        private ManualResetEventSlim msgsRecievedGate = new ManualResetEventSlim(false);

        public IConnection ConnectRabbitMQ()
        {
            EnvVariablesHandler envHandler = new EnvVariablesHandler();
            RabbitMqConfiguration rabbitMqConfiguration = envHandler.GetRabbitEnvVariables();

            var connectionFactory = new ConnectionFactory
            {
                HostName = rabbitMqConfiguration.RabbitMqHost,
                Port = Convert.ToInt32(rabbitMqConfiguration.RabbitMqPort),
                UserName = rabbitMqConfiguration.RabbitMqUsername,
                Password = rabbitMqConfiguration.RabbitMqPassword
            };

            IConnection rabbitConnection = connectionFactory.CreateConnection();

            return rabbitConnection;

        }

        public List<QueueMessage> ConsumeQueue(string queue)
        {
            try
            {
                IConnection rabbitConnection = ConnectRabbitMQ();

                using (var channel = rabbitConnection.CreateModel())
                {
                    var queueResult = channel.QueueDeclare(queue: queue,
                                         durable: true,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null);

                    channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

                    var consumer = new EventingBasicConsumer(channel);

                    uint msgCount = queueResult.MessageCount;
                    uint counter = 0;

                    consumer.Received += (sender, ea) =>
                    {
                        counter++;

                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        QueueMessage queueMsg = JsonConvert.DeserializeObject<QueueMessage>(message);
                        messageList.Add(queueMsg);

                        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);

                        if (msgCount == counter)
                        {
                            msgsRecievedGate.Set();

                            return;
                        }

                    };

                    channel.BasicConsume(queue: queue,
                                         autoAck: false,
                                         consumer: consumer);

                    // Wait here until all messages are retrieved
                    msgsRecievedGate.Wait();

                    QueueLog queueLog = new QueueLog()
                    {
                        Date = DateTime.Now,
                        Message = JsonConvert.SerializeObject(""),
                        QueueName = queue
                    };

                    string logText = $"{JsonConvert.SerializeObject(queueLog)}";
                    log.Info(logText);

                    return messageList;
                }
            }
            catch (Exception exception)
            {
                QueueLog queueLog = new QueueLog()
                {
                    OperationType = "BasicConsume",
                    Date = DateTime.Now,
                    Message = JsonConvert.SerializeObject(""),
                    QueueName = queue,
                    ExceptionMessage = exception.Message.ToString()
                };

                string logText = $"Exception: {JsonConvert.SerializeObject(queueLog)}";
                log.Info(logText);

                return null;
            }
        }

        public void QueueMessageDirect(TMessage message, string queue, string exchange, string routingKey)
        {
            try
            {
                IConnection rabbitConnection = ConnectRabbitMQ();
                var channel = rabbitConnection.CreateModel();
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true;

                channel.QueueDeclare(queue: queue,
                                     durable: true,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                string serializedObj = JsonConvert.SerializeObject(message);
                var body = Encoding.UTF8.GetBytes(serializedObj);

                channel.BasicPublish(exchange: exchange,
                                     routingKey: routingKey,
                                     basicProperties: properties,
                                     body: body);

                QueueLog queueLog = new QueueLog()
                {
                    OperationType = "BasicPublish",
                    Date = DateTime.Now,
                    ExchangeName = exchange,
                    Message = JsonConvert.SerializeObject(message),
                    QueueName = queue,
                    RoutingKey = routingKey
                };

                string logText = $"{JsonConvert.SerializeObject(queueLog)}";
                log.Info(logText);

            }
            catch (Exception exception)
            {
                QueueLog queueLog = new QueueLog()
                {
                    OperationType = "BasicPublish",
                    Date = DateTime.Now,
                    ExchangeName = exchange,
                    Message = JsonConvert.SerializeObject(message),
                    QueueName = queue,
                    RoutingKey = routingKey,
                    ExceptionMessage = exception.Message.ToString()
                };

                string logText = $"Exception: {JsonConvert.SerializeObject(queueLog)}";
                log.Error(logText);

            }
        }
    }
}
