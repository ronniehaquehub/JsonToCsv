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
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Configuration;

namespace JsonToCsv
{
    public static class D365OrderForShipment
    {
        [FunctionName("D365OrderForShipment")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
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
                string strFileName = $"BC_D365OrderToShip_{nzDateTime:yyyyMMdd_HHmmss.fff}_{controlNumber}";
                string csvFileName = $"/bluecorp-incoming/{strFileName}.csv";
                
                //Upload the CSV to the SFTP server
                UploadCsvToSftp(csvOutput.ToString(), csvFileName);

                string blobConnStringSecretName = Environment.GetEnvironmentVariable("BlobConnStringSecretName");
                string blobContainerNameSecretName = Environment.GetEnvironmentVariable("BlobContainerNameSecretName");

                var secretClient = GetSecretClient();
                string blobConnString = secretClient.GetSecret(blobConnStringSecretName).Value.Value.ToString();
                string blobContainerName = secretClient.GetSecret(blobContainerNameSecretName).Value.Value.ToString();
                string blobName = $"{strFileName}.json"; // Unique file name

                // Create the Blob client
                BlobServiceClient blobServiceClient = new BlobServiceClient(blobConnString);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);

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

