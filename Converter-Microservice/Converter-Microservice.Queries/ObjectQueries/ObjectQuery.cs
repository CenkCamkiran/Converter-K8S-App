﻿using ConverterMicroservice.Models;
using MediatR;

namespace Converter_Microservice.Queries.ObjectQueries
{
    public class ObjectQuery : IRequest<ObjectData>
    {
        public string BucketName { get; set; }
        public string ObjectName { get; set; }

        public ObjectQuery(string bucketName, string objectName)
        {
            BucketName = bucketName;
            ObjectName = objectName;
        }
    }
}
