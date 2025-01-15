using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Data;
using SuperConvert.Extensions;
using System.Text;
using System.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.AspNetCore.Hosting.Server;
using System.Web;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Globalization;
using static JsonToCsv.Class1;
using Azure.Storage.Blobs;

namespace JsonToCsv
{
    public static class D365OrderForShipment
    {
        [FunctionName("D365OrderForShipment")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP BlueCorp trigger function processed a request.");

            try
            {

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

                if (string.IsNullOrEmpty(requestBody))
                {
                    return new BadRequestObjectResult("Request body cannot be empty.");
                }

                // Parse the JSON input
                dynamic jsonPayload = JsonConvert.DeserializeObject(requestBody);

                // Prepare the CSV content
                var csvOutput = new StringBuilder();
                var csvWriter = new CsvHelper(csvOutput);

                // Write CSV headers
                csvWriter.WriteRow(new List<string>
                {
                    "CustomerReference", "LoadId", "ContainerType", "ItemCode", "ItemQuantity", "ItemWeight",
                    "Street", "City", "State", "PostalCode", "Country"
                });

                // Iterate over containers and items
                string controlNumber = (string)jsonPayload.controlNumber;
                foreach (var container in jsonPayload.containers)
                {
                    string containerType = MapContainerType((string)container.containerType);
                    foreach (var item in container.items)
                    {
                        csvWriter.WriteRow(new List<string>
                        {
                            (string)jsonPayload.salesOrder,
                            (string)container.loadId,
                            containerType,
                            (string)item.itemCode,
                            item.quantity.ToString(),
                            item.cartonWeight.ToString(),
                            (string)jsonPayload.deliveryAddress.street,
                            (string)jsonPayload.deliveryAddress.city,
                            (string)jsonPayload.deliveryAddress.state,
                            (string)jsonPayload.deliveryAddress.postalCode,
                            (string)jsonPayload.deliveryAddress.country
                        });
                    }
                }

                if (string.IsNullOrWhiteSpace(csvOutput.ToString()))
                {
                    return new BadRequestObjectResult("CSV content is empty.");
                }

                //Generate a unique file name
                var nzTimeZone = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
                var utcOffset = new DateTimeOffset(DateTime.UtcNow, TimeSpan.Zero);
                DateTime nzDateTime = DateTime.UtcNow.Add(nzTimeZone.GetUtcOffset(utcOffset));
                string now = $"BC_D365OrderToShip_{controlNumber}_{nzDateTime:yyyyMMdd_HHmmss.fff}";
                string csvFileName = $"/bluecorp-incoming/{now}.csv";
                
                //Upload the CSV to the SFTP server
                UploadCsvToSftp(csvOutput.ToString(), csvFileName);

                string connectionString = "DefaultEndpointsProtocol=https;AccountName=stbluecorpappdev;AccountKey=RfEP8dnoWIVnSdPUCka/GvV9uxk/7s75Fw0CFl3LpaOzhWOUWDHh62p4/YXGZyzJ5npsRuZXmNGt+AStkVWxKw==;EndpointSuffix=core.windows.net";
                string containerName = "bluecorp-incoming"; // Replace with your container name
                string blobName = $"{now}.json"; // Unique file name

                // Create the Blob client
                BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

                // Ensure the container exists
                containerClient.CreateIfNotExistsAsync();

                // Get a BlobClient for the new blob
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                // Upload the JSON string to the blob
                string jsonString = jsonPayload.ToString();
                using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonString)))
                {
                    blobClient.Upload(stream, overwrite: true);
                }

                return new OkObjectResult("CSV uploaded to SFTP server and JSON uploaded to blob container");

            }
            catch (Exception ex)
            {
                return new ContentResult
                {
                    Content = $"Error processing request: {ex.Message}",
                    StatusCode = 500
                };
            }
        }
    }
}

