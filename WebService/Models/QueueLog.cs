﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public class QueueLog
    {
        public string QueueName { get; set; } = String.Empty;
        public string Exchange { get; set; } = String.Empty; 
        public string RoutingKey { get; set; } = String.Empty; 
        public QueueMessage? Message { get; set; } = null; 
        public DateTime DateTime { get; set; }

    }
}
