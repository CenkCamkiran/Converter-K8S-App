﻿using Converter_Microservice.Common.Constants;
using Converter_Microservice.Common.Events;
using Converter_Microservice.Repositories.Interfaces;
using ConverterMicroservice.Models;
using log4net.Core;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel;
using Newtonsoft.Json;

namespace Converter_Microservice.Repositories.Repositories
{
    public class ObjectRepository : IObjectRepository
    {
        private readonly IMinioClient _minioClient;
        private readonly Lazy<IQueueRepository> _queueErrorRepository;
        private readonly Lazy<IQueueRepository> _queueOtherRepository;
        private readonly ILogger<ObjectRepository> _logger;

        public ObjectRepository(IMinioClient minioClient, Lazy<IQueueRepository> queueErrorRepository, Lazy<IQueueRepository> queueOtherRepository, ILogger<ObjectRepository> logger)
        {
            _minioClient = minioClient;
            _queueErrorRepository = queueErrorRepository;
            _queueOtherRepository = queueOtherRepository;
            _logger = logger;
        }

        public async Task<bool> PutObjectAsync(string bucketName, string objectName, Stream stream, string contentType)
        {
            IServerSideEncryption? sse = null;
            stream.Position = 0;

            Dictionary<string, string> metadata = new Dictionary<string, string>()
            {
                {
                    "FileByteLength", stream.Length.ToString()
                },
                {
                    "ContentType", contentType
                },
                {
                    "ModifiedDate", DateTime.Now.ToString()
                }
            };

            try
            {
                var beArgs = new BucketExistsArgs()
                    .WithBucket(bucketName);
                bool found = await _minioClient.BucketExistsAsync(beArgs).ConfigureAwait(false);
                if (!found)
                {
                    var mbArgs = new MakeBucketArgs()
                        .WithBucket(bucketName);
                    await _minioClient.MakeBucketAsync(mbArgs).ConfigureAwait(false);
                }

                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithStreamData(stream)
                    .WithObjectSize(stream.Length)
                    .WithContentType(contentType)
                    .WithHeaders(metadata)
                    .WithServerSideEncryption(sse);
                await _minioClient.PutObjectAsync(putObjectArgs).ConfigureAwait(false);

                ObjectStorageLog objectStorageLog = new ObjectStorageLog()
                {
                    OperationType = LogEvents.PutObjectEventMessage,
                    BucketName = bucketName,
                    ContentLength = stream.Length,
                    ContentType = contentType,
                    ObjectName = objectName,
                    Date = DateTime.Now
                };
                OtherLog otherLog = new OtherLog()
                {
                    storageLog = objectStorageLog
                };
                _queueOtherRepository.Value.QueueMessageDirect(otherLog, ProjectConstants.OtherLogsServiceQueueName, ProjectConstants.OtherLogsServiceExchangeName, ProjectConstants.OtherLogsServiceRoutingKey, ProjectConstants.OtherLogsServiceExchangeTtl);
                _logger.LogInformation(LogEvents.PutObjectEvent, LogEvents.PutObjectEventMessage);

                return await Task.FromResult(true);

            }
            catch (Exception exception)
            {
                ObjectStorageLog objectStorageLog = new ObjectStorageLog()
                {
                    OperationType = LogEvents.PutObjectEventMessage,
                    BucketName = bucketName,
                    ContentLength = stream.Length,
                    ContentType = contentType,
                    ObjectName = objectName,
                    Date = DateTime.Now,
                    ExceptionMessage = exception.Message.ToString()
                };
                ErrorLog errorLog = new ErrorLog()
                {
                    storageLog = objectStorageLog
                };
                _queueErrorRepository.Value.QueueMessageDirect(errorLog, ProjectConstants.ErrorLogsServiceQueueName, ProjectConstants.ErrorLogsServiceExchangeName, ProjectConstants.ErrorLogsServiceRoutingKey, ProjectConstants.ErrorLogsServiceExchangeTtl);
                _logger.LogError(LogEvents.PutObjectEvent, exception.Message.ToString());

                throw;
            }
        }

        public async Task<ObjectData> GetObjectAsync(string bucketName, string objectName)
        {
            ObjectData? objDataModel = null;

            try
            {
                var beArgs = new BucketExistsArgs()
                    .WithBucket(bucketName);
                bool found = await _minioClient.BucketExistsAsync(beArgs).ConfigureAwait(false);
                if (!found)
                {
                    var mbArgs = new MakeBucketArgs()
                        .WithBucket(bucketName);
                    await _minioClient.MakeBucketAsync(mbArgs).ConfigureAwait(false);
                }

                string mp4FileFullPath = Path.Combine(Path.GetTempPath(), objectName + ".mp4");
                var args = new GetObjectArgs()
                               .WithBucket(bucketName)
                               .WithObject(objectName)
                               .WithFile(mp4FileFullPath);

                ObjectStat objStat = await _minioClient.GetObjectAsync(args);

                objDataModel = new ObjectData()
                {
                    Mp4FileFullPath = mp4FileFullPath,
                    ObjectStats = objStat
                };

                ObjectStorageLog objectStorageLog = new ObjectStorageLog()
                {
                    OperationType = LogEvents.GetObjectEventMessage,
                    BucketName = bucketName,
                    ContentLength = objStat != null ? objStat.Size : 0,
                    ObjectName = objectName,
                    Date = DateTime.Now,
                    ContentType = objStat != null ? objStat.ContentType : ""
                };
                OtherLog otherLog = new OtherLog()
                {
                    storageLog = objectStorageLog
                };
                _queueOtherRepository.Value.QueueMessageDirect(otherLog, ProjectConstants.OtherLogsServiceQueueName, ProjectConstants.OtherLogsServiceExchangeName, ProjectConstants.OtherLogsServiceRoutingKey, ProjectConstants.OtherLogsServiceExchangeTtl);
                _logger.LogInformation(LogEvents.GetObjectEvent, LogEvents.GetObjectEventMessage);

            }
            catch (Exception exception)
            {
                ObjectStorageLog objectStorageLog = new ObjectStorageLog()
                {
                    OperationType = LogEvents.GetObjectEventMessage,
                    BucketName = bucketName,
                    ObjectName = objectName,
                    Date = DateTime.Now,
                    ExceptionMessage = exception.Message.ToString()
                };
                ErrorLog errorLog = new ErrorLog()
                {
                    storageLog = objectStorageLog
                };
                _queueErrorRepository.Value.QueueMessageDirect(errorLog, ProjectConstants.ErrorLogsServiceQueueName, ProjectConstants.ErrorLogsServiceExchangeName, ProjectConstants.ErrorLogsServiceRoutingKey, ProjectConstants.ErrorLogsServiceExchangeTtl);
                _logger.LogError(LogEvents.GetObjectEvent, exception.Message.ToString());

                throw;
            }

            return objDataModel;
        }
    }
}
