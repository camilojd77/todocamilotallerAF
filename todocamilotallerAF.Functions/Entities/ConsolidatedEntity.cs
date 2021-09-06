using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace todocamilotallerAF.Functions.Entities
{
    public class ConsolidatedEntity : TableEntity
    {
        public int? EmployedId { get; set; }

        public DateTime? Date { get; set; }

        public double? WorkedMinutes { get; set; }
    }
}
