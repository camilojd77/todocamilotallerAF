using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using todocamilotallerAF.Functions.Entities;

namespace todocamilotallerAF.Functions.Functions
{
    public static class ScheduledFunction
    {
        [FunctionName("ScheduledFunction")]
        public static async Task Run(
            [TimerTrigger("0 */60 * * * *")] TimerInfo myTimer,
            [Table("time", Connection = "AzureWebJobsStorage")] CloudTable timeTable,
            [Table("consolidated", Connection = "AzureWebJobsStorage")] CloudTable consolidatedTable,
            ILogger log)
        {
            log.LogInformation($"Consolidating completed function executed at: {DateTime.Now}");

            string filter = TableQuery.GenerateFilterConditionForBool("IsConsolidated", QueryComparisons.Equal, false);
            TableQuery<TimeEntity> query = new TableQuery<TimeEntity>().Where(filter);
            TableQuerySegment<TimeEntity> completedConsolidated = await timeTable.ExecuteQuerySegmentedAsync(query, null);
            if (completedConsolidated.Results.Count > 0)
            {
                List<TimeEntity> timesTable = completedConsolidated.OrderBy(time => time.EmployedId).ThenBy(time => time.Date).ToList();

                TimeSpan DateDifference;
                double WorkedMinutes = 0;
                string LastRowKey = timesTable.Last().RowKey;

                //Assign the first EmployedId
                int IdAux = timesTable.FirstOrDefault().EmployedId;

                for (int i = 0; i < timesTable.Count; i++)
                {
                    if (IdAux != timesTable[i].EmployedId)
                    {
                        CreateOrUpdateConsolidation(IdAux, WorkedMinutes, consolidatedTable, timesTable[i - 1].Date);
                        IdAux = timesTable[i].EmployedId;
                        WorkedMinutes = 0;
                    }

                    if(timesTable[i].Type == 1)
                    {
                        DateDifference = timesTable[i].Date - timesTable[i - 1].Date;
                        WorkedMinutes += DateDifference.TotalMinutes;

                        UpdateIsConsolidated(timesTable[i - 1].RowKey, timeTable);
                        UpdateIsConsolidated(timesTable[i].RowKey, timeTable);

                        if (LastRowKey == timesTable[i].RowKey)
                        {
                            CreateOrUpdateConsolidation(IdAux, WorkedMinutes, consolidatedTable, timesTable[i - 1].Date);
                        }
                    }

                    //If the last register has Type = 0, then Create the before consolidated.
                    if (LastRowKey == timesTable[i].RowKey && timesTable[i].Type == 0)
                    {
                        CreateOrUpdateConsolidation(IdAux, WorkedMinutes, consolidatedTable, timesTable[i - 1].Date);
                        break;
                    }

                    //If the EmployedId is the same but the dates are from different days, then Create a new consolidated register
                    if (i > 0)
                    {
                        if (!timesTable[i - 1].Date.ToString("dd-MM-yyyy").Equals(timesTable[i].Date.ToString("dd-MM-yyyy")) && IdAux == timesTable[i].EmployedId)
                        {
                            CreateOrUpdateConsolidation(IdAux, WorkedMinutes, consolidatedTable, timesTable[i - 1].Date);
                            WorkedMinutes = 0;
                        }
                    }

                }
            }
        }

        private static async void CreateConsolidation(int IdEmployed, double WorkedMinutes, CloudTable consolidatedTable, DateTime employedDate)
        {
            ConsolidatedEntity ConsolidatedEntity = new ConsolidatedEntity
            {
                EmployedId = IdEmployed,
                Date = new DateTime(employedDate.Year, employedDate.Month, employedDate.Day, 00, 00, 0),
                WorkedMinutes = WorkedMinutes,
                ETag = "*",
                PartitionKey = "CONSOLIDATED",
                RowKey = Guid.NewGuid().ToString(),
            };

            if (ConsolidatedEntity?.EmployedId != null && ConsolidatedEntity?.Date != null && ConsolidatedEntity?.WorkedMinutes != null)
            {
                if (ConsolidatedEntity?.WorkedMinutes > 0)
                {
                    TableOperation addOperation = TableOperation.Insert(ConsolidatedEntity);
                    await consolidatedTable.ExecuteAsync(addOperation);
                }
            }
        }

        private static async void UpdateExistConsolidated(string rowkey, CloudTable consolidatedTable, double WorkedMinutes)
        {
            TableOperation findOperation = TableOperation.Retrieve<ConsolidatedEntity>("CONSOLIDATED", rowkey);
            TableResult findResult = await consolidatedTable.ExecuteAsync(findOperation);

            ConsolidatedEntity consolidated_Entity = (ConsolidatedEntity)findResult.Result;
            consolidated_Entity.WorkedMinutes += WorkedMinutes;

            TableOperation add_Operation = TableOperation.Replace(consolidated_Entity);
            await consolidatedTable.ExecuteAsync(add_Operation);
        }

        private static async void CreateOrUpdateConsolidation(int IdEmployed, double WorkedMinutes, CloudTable consolidatedTable, DateTime employedDate)
        {
            string QueryByID = TableQuery.GenerateFilterConditionForInt("EmployedId", QueryComparisons.Equal, IdEmployed);
            TableQuery<ConsolidatedEntity> Query = new TableQuery<ConsolidatedEntity>().Where(QueryByID);
            TableQuerySegment<ConsolidatedEntity> existConsolidated = await consolidatedTable.ExecuteQuerySegmentedAsync(Query, null);

            if (existConsolidated.Results.Count != 0)
            {
                foreach (ConsolidatedEntity consolidatedEntity in existConsolidated)
                {
                    if (((DateTime)consolidatedEntity.Date).ToString("dd-MM-yyyy").Equals(employedDate.Date.ToString("dd-MM-yyyy")))
                    {
                        UpdateExistConsolidated(consolidatedEntity.RowKey, consolidatedTable, WorkedMinutes);
                    }
                    else
                    {
                        CreateConsolidation(IdEmployed, WorkedMinutes, consolidatedTable, employedDate);
                    }
                }
            }
            else
            {
                CreateConsolidation(IdEmployed, WorkedMinutes, consolidatedTable, employedDate);
            }
        }

        private static async void UpdateIsConsolidated(string rowkey, CloudTable timeTable)
        {
            //Search in Time table the current RowKey
            TableOperation findOperation = TableOperation.Retrieve<TimeEntity>("TIME", rowkey);
            TableResult findResult = await timeTable.ExecuteAsync(findOperation);

            //If there is a row, set timeEntity = true
            TimeEntity timeEntity = (TimeEntity)findResult.Result;
            timeEntity.IsConsolidated = true;

            //Update IsConsolidated = true in Time table
            TableOperation add_Operation = TableOperation.Replace(timeEntity);
            await timeTable.ExecuteAsync(add_Operation);
        }
    }
}
