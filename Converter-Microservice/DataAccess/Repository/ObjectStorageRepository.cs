﻿using Configuration;
using DataAccess.Interfaces;
using Minio;
using Minio.DataModel;
using Models;
using Newtonsoft.Json;

namespace DataAccess.Repository
{
    public class ObjectStorageRepository: IObjectStorageRepository
    {
        private Log4NetRepository log = new Log4NetRepository();

        private MinioClient ConnectMinio()
        {
            EnvVariablesHandler envVariablesHandler = new EnvVariablesHandler();
            MinioConfiguration minioConfiguration = envVariablesHandler.GetMinioEnvVariables();

            MinioClient minioClient = new MinioClient()
                                    .WithEndpoint(minioConfiguration.MinioHost)
                                    .WithCredentials(minioConfiguration.MinioAccessKey, minioConfiguration.MinioSecretKey)
                                    .WithSSL(false);

            return minioClient;
        }

        public async Task StoreFileAsync(string bucketName, string objectName, Stream stream, string contentType)
        {

            MinioClient minioClient = ConnectMinio();

            ServerSideEncryption? sse = null;
            stream.Position = 0;

            Dictionary<string, string> metadata = new Dictionary<string, string>()
            {
                {
                    "Id", objectName
                },
                {
                    "FileLength", stream.Length.ToString()
                },
                {
                    "ContentType", contentType
                }
            };

            try
            {
                var beArgs = new BucketExistsArgs()
                    .WithBucket(bucketName);
                bool found = await minioClient.BucketExistsAsync(beArgs).ConfigureAwait(false);
                if (!found)
                {
                    var mbArgs = new MakeBucketArgs()
                        .WithBucket(bucketName);
                    await minioClient.MakeBucketAsync(mbArgs).ConfigureAwait(false);
                }

                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithStreamData(stream)
                    .WithObjectSize(stream.Length)
                    .WithContentType("video/mp4")
                    .WithHeaders(metadata)
                    .WithServerSideEncryption(sse);
                await minioClient.PutObjectAsync(putObjectArgs).ConfigureAwait(false);
                //await _minioClient.Build().PutObjectAsync(bucketName, objectName, stream, stream.Length, contentType).ConfigureAwait(false);

                ObjectStorageLog objectStorageLog = new ObjectStorageLog()
                {
                    OperationType = nameof(minioClient.PutObjectAsync),
                    BucketName = bucketName,
                    ContentLength = stream.Length,
                    ContentType = contentType,
                    ObjectName = objectName,
                    Date = DateTime.Now
                };
                LogOtherRepository logOtherRepository = new LogOtherRepository();
                await logOtherRepository.LogStorageOther(objectStorageLog);

            }
            catch (Exception exception)
            {
                ObjectStorageLog objectStorageLog = new ObjectStorageLog()
                {
                    OperationType = nameof(minioClient.PutObjectAsync),
                    BucketName = bucketName,
                    ContentLength = stream.Length,
                    ContentType = contentType,
                    ObjectName = objectName,
                    Date = DateTime.Now,
                    ExceptionMessage = exception.Message.ToString()
                };

                QueueRepository<ObjectStorageLog> queueHandler = new QueueRepository<ObjectStorageLog>();
                queueHandler.QueueMessageDirect(objectStorageLog, "errorlogs", "log_exchange.direct", "error_log");

                string logText = $"Exception: {JsonConvert.SerializeObject(objectStorageLog)}";
                log.Info(logText);
            }
        }

        public async Task<ObjectDataModel> GetFileAsync(string bucketName, string objectName)
        {

            ObjectDataModel? objDataModel = null;
            ServerSideEncryption? sse = null;
            MinioClient minioClient = ConnectMinio();

            try
            {
                var beArgs = new BucketExistsArgs()
                    .WithBucket(bucketName);
                bool found = await minioClient.Build().BucketExistsAsync(beArgs).ConfigureAwait(false);
                if (!found)
                {
                    var mbArgs = new MakeBucketArgs()
                        .WithBucket(bucketName);
                    await minioClient.Build().MakeBucketAsync(mbArgs).ConfigureAwait(false);
                }

                string filePath = Path.Combine(Path.GetTempPath(), objectName + ".mp4");
                var args = new GetObjectArgs()
                               .WithBucket(bucketName)
                               .WithObject(objectName)
                               .WithFile(filePath);

                ObjectStat objStat = await minioClient.GetObjectAsync(args);

                objDataModel = new ObjectDataModel()
                {
                    FileFullPath = filePath,
                    ObjectStats = objStat
                };

                ObjectStorageLog objectStorageLog = new ObjectStorageLog()
                {
                    OperationType = "SelectObjectContentAsync",
                    BucketName = bucketName,
                    ContentLength = objStat != null ? objStat.Size : 0,
                    ObjectName = objectName,
                    Date = DateTime.Now,
                    ContentType = objStat != null ? objStat.ContentType : ""
                };
                LogOtherRepository logOtherRepository = new LogOtherRepository();
                await logOtherRepository.LogStorageOther(objectStorageLog);

            }
            catch (Exception exception)
            {
                ObjectStorageLog objectStorageLog = new ObjectStorageLog()
                {
                    OperationType = "SelectObjectContentAsync",
                    BucketName = bucketName,
                    ObjectName = objectName,
                    Date = DateTime.Now,
                    ExceptionMessage = exception.Message.ToString()
                };

                QueueRepository<ObjectStorageLog> queueHandler = new QueueRepository<ObjectStorageLog>();
                queueHandler.QueueMessageDirect(objectStorageLog, "errorlogs", "log_exchange.direct", "error_log");

                string logText = $"Exception: {JsonConvert.SerializeObject(objectStorageLog)}";
                log.Error(logText);

            }

            return objDataModel;
        }
    }
}
