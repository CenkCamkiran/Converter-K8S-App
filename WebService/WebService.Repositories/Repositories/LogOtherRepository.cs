﻿using WebService.Models;
using WebService.Repositories.Interfaces;

namespace WebService.Repositories.Repositories
{
    public partial class LogOtherRepository : ILogOtherRepository
    {
        private readonly ILog4NetRepository _log4NetRepository;
        private readonly ILogRepository<QueueLog> _loggingRepositoryQueue;
        private readonly ILogRepository<ObjectStorageLog> _loggingRepositoryObjStorageLog;

        public LogOtherRepository(ILog4NetRepository log4NetRepository, ILogRepository<QueueLog> loggingRepositoryQueue, ILogRepository<ObjectStorageLog> loggingRepositoryObjStorageLog)
        {
            _log4NetRepository = log4NetRepository;
            _loggingRepositoryQueue = loggingRepositoryQueue;
            _loggingRepositoryObjStorageLog = loggingRepositoryObjStorageLog;
        }

        public async Task LogQueueOther(QueueLog queueLog)
        {
            await _loggingRepositoryQueue.IndexDocAsync("webservice_queue_logs", queueLog);

            string logText = $"Exchange: {queueLog.ExchangeName} - Queue: {queueLog.QueueName} - Routing Key: {queueLog.RoutingKey} - Message: (fileGuid: {queueLog.Message.fileGuid} && email: {queueLog.Message.email})";
            _log4NetRepository.Info(logText);
        }

        public async Task LogStorageOther(ObjectStorageLog objectStorageLog)
        {
            await _loggingRepositoryObjStorageLog.IndexDocAsync("webservice_objstorage_logs", objectStorageLog);

            string logText = $"BucketName: {objectStorageLog.BucketName} - ObjectName: {objectStorageLog.ObjectName} - Content Type: {objectStorageLog.ContentType}";
            _log4NetRepository.Info(logText);
        }
    }
}