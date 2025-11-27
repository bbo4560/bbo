using System;

namespace TEST2
{
    public class SystemLog
    {
        public int Id { get; set; }
        public DateTime OperationTime { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public string AffectedData { get; set; } = string.Empty;
        public string DetailDescription { get; set; } = string.Empty;
    }
}

