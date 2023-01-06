﻿using Helpers.Interfaces;
using Models;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.NetworkInformation;

namespace Helpers
{
    public class PingHelper : IPingHelper
    {

        public PingReply PingVMHost()
        {
            Uri uri = new Uri(Environment.GetEnvironmentVariable("ELK_HOST"));

            try
            {
                Ping ping = new Ping();
                Task<PingReply> pingResult = ping.SendPingAsync(uri.Host, 60000);

                return pingResult.Result;
            }
            catch (Exception exception)
            {
                UploadMp4Response error = new UploadMp4Response();
                error.ErrorMessage = exception.Message.ToString();
                error.ErrorCode = (int)HttpStatusCode.InternalServerError;

                throw new WebServiceException(JsonConvert.SerializeObject(error));
            }

        }

    }
}
