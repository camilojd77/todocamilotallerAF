using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using todocamilotallerAF.Common.Models;
using todocamilotallerAF.Common.Responses;
using todocamilotallerAF.Functions.Entities;

namespace todocamilotallerAF.Functions.Functions
{
    public static class ConsolidatedApi
    {
        //---------------------------- GET ALL CONSOLIDATES BY DATE ----------------------------------------
        [FunctionName(nameof(GetAllConsolidatesByDate))]
        public static async Task<IActionResult> GetAllConsolidatesByDate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "consolidated/{date}")] HttpRequest req,
            [Table("consolidated", "CONSOLIDATED", Connection = "AzureWebJobsStorage")] CloudTable consolidatedTable,
            DateTime date,
            ILogger log)
        {
            log.LogInformation($"Get consolidated by date: {date} received.");

            TableQuery<ConsolidatedEntity> query = new TableQuery<ConsolidatedEntity>();
            TableQuerySegment<ConsolidatedEntity> consolidates = await consolidatedTable.ExecuteQuerySegmentedAsync(query, null);

            if (consolidates.Results.Count <= 0)
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "Consolidates not found."
                });
            }

            //Obtains the consolidated ones that coincide with the date entered
            List<ConsolidatedEntity>  consolidatedList = consolidates.Where(consolidate => ((DateTime)consolidate.Date).ToString("dd-MM-yyyy").Equals(date.Date.ToString("dd-MM-yyyy"))).ToList();
            if (consolidatedList.Count == 0)
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = $"The date: {date} has not consolidates."
                });
            }

            string message = $"Consolidated date: {date}, received.";

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = consolidatedList
            });
        }
    }
}
