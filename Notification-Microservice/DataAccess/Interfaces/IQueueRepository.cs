﻿using Models;

namespace DataAccess.Interfaces
{
    public interface IQueueRepository<TMessage> where TMessage : class
    {
        List<QueueMessage> ConsumeQueue(string queue);
    }
}