﻿using DataLayer.Interfaces;
using Helpers;
using Models;
using Nest;
using Newtonsoft.Json;
using System.Net;

namespace DataLayer.DataAccess
{
    public class LoggingRepository<TModel> : ILoggingRepository<TModel> where TModel : class
    {
        private readonly IElasticClient _elasticClient;
        private readonly ILog4NetRepository _log4NetRepository;

        public LoggingRepository(IElasticClient elasticClient, ILog4NetRepository log4NetRepository)
        {
            _elasticClient = elasticClient;
            _log4NetRepository = log4NetRepository;
        }

        public async Task<bool> IndexDocAsync(string indexName, TModel model)
        {
            try
            {
                //IndexResponse indexDocument = await _elasticClient.IndexDocumentAsync(model);
                IndexResponse indexDocument = await _elasticClient.IndexAsync(model, elk => elk.Index(indexName));

                string elkResponse = $"Doc ID: {indexDocument.Id} - Index: {indexDocument.Index} - Result: {indexDocument.Result} - Is Valid: {indexDocument.IsValid} - ApiCall.HttpStatusCode: {indexDocument.ApiCall.HttpStatusCode} - ApiCall.Success: {indexDocument.ApiCall.Success}";
                _log4NetRepository.Info(elkResponse);

                return indexDocument.IsValid;

            }
            catch (Exception exception)
            {
                WebServiceErrors error = new WebServiceErrors();
                error.ErrorMessage = exception.Message.ToString();
                error.ErrorCode = (int)HttpStatusCode.InternalServerError;

                _log4NetRepository.Error(exception.Message.ToString());

                throw new WebServiceException(JsonConvert.SerializeObject(error));
            }
        }
    }
}
