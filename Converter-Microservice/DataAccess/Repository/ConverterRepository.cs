﻿using DataAccess.Interfaces;
using Models;
using Newtonsoft.Json;
using Xabe.FFmpeg;

namespace DataAccess.Repository
{
    public class ConverterRepository : IConverterRepository
    {
        private readonly ILog4NetRepository _log4NetRepository;
        private readonly IObjectStorageRepository _objectStorageRepository;
        private readonly Lazy<IQueueRepository<QueueMessage>> _queueRepository;
        private readonly Lazy<IQueueRepository<ErrorLog>> _queueErrorRepository;
        private readonly Lazy<IQueueRepository<OtherLog>> _queueOtherRepository;

        public ConverterRepository(ILog4NetRepository log4NetRepository, IObjectStorageRepository objectStorageRepository, Lazy<IQueueRepository<QueueMessage>> queueRepository, Lazy<IQueueRepository<ErrorLog>> queueErrorRepository, Lazy<IQueueRepository<OtherLog>> queueOtherRepository)
        {
            _log4NetRepository = log4NetRepository;
            _objectStorageRepository = objectStorageRepository;
            _queueRepository = queueRepository;
            _queueErrorRepository = queueErrorRepository;
            _queueOtherRepository = queueOtherRepository;
        }

        public async Task<QueueMessage> ConvertMP4_to_MP3_Async(ObjectDataModel objDataModel, QueueMessage message) //string ConvertFromFilePath, string ConvertToFilePath
        {
            QueueMessage? msg = null;

            try
            {
                string guid = Guid.NewGuid().ToString();
                string ConvertToFilePath = Path.Combine(Path.GetTempPath(), guid + ".mp3");

                var conversion = await FFmpeg.Conversions.FromSnippet.ExtractAudio(objDataModel.FileFullPath, ConvertToFilePath);
                conversion.SetOverwriteOutput(false);

                await conversion.Start();

                using (FileStream fs = File.OpenRead(ConvertToFilePath))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        await fs.CopyToAsync(ms);
                        await _objectStorageRepository.StoreFileAsync("audios", guid, ms, "audio/mp3");

                        msg = new QueueMessage()
                        {
                            email = message.email,
                            fileGuid = guid
                        };

                        _queueRepository.Value.QueueMessageDirect(msg, "notification", "notification_exchange.direct", "mp4_to_notif");

                    }
                }

                if(File.Exists(ConvertToFilePath))
                    File.Delete(ConvertToFilePath);

                ConverterLog converterLog = new ConverterLog()
                {
                    Info = "Conversion finished!",
                    Date = DateTime.Now
                };
                OtherLog otherLog = new OtherLog()
                {
                    converterLog = converterLog
                };
                _queueOtherRepository.Value.QueueMessageDirect(otherLog, "otherlogs", "log_exchange.direct", "other_log");

                string logText = $"{JsonConvert.SerializeObject(otherLog)}";
                _log4NetRepository.Info(logText);

                return msg;

            }
            catch (Exception exception)
            {
                ConverterLog exceptionModel = new ConverterLog()
                {
                    Error = exception.Message.ToString(),
                    Date = DateTime.Now
                };
                ErrorLog errorLog = new ErrorLog()
                {
                    converterLog = exceptionModel
                };
                _queueErrorRepository.Value.QueueMessageDirect(errorLog, "errorlogs", "log_exchange.direct", "error_log");

                string logText = $"Exception: {JsonConvert.SerializeObject(errorLog)}";
                _log4NetRepository.Error(logText);

                return msg;
            }
        }
    }
}
