using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace todocamilotallerAF.Functions.Entities
{
    internal class TimeEntity : TableEntity
    {
        public int EmployedId { get; set; }

        public DateTime Date { get; set; }

        public int Type { get; set; }

        public bool IsConsolidated { get; set; }
    }
}
