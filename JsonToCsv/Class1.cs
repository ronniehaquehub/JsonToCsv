using Azure.Storage.Blobs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Data;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonToCsv
{
    public class Class1
    {
        public class CsvHelper
        {
            private readonly StringBuilder _output;

            public CsvHelper(StringBuilder output)
            {
                _output = output;
            }

            public void WriteRow(IEnumerable<string> fields)
            {
                string row = string.Join(",", fields.Select(f => $"\"{f.Replace("\"", "\"\"")}\""));
                _output.AppendLine(row);
            }
        }


        public static string MapContainerType(string containerType)
        {
            var mapping = new Dictionary<string, string>
            {
                { "20RF", "REF20" },
                { "40RF", "REF40" },
                { "20HC", "HC20" },
                { "40HC", "HC40" }
            };

            return mapping.TryGetValue(containerType, out string mappedType) ? mappedType : containerType;
        }

        public static string ConvertJsonToCsv(dynamic jsonPayload)
        {
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

            return csvOutput.ToString();
        }

        //public static string ConvertJsonToCsv(JArray jsonArray)
        //{
        //    using (var stringWriter = new StringWriter())
        //    using (var csvWriter = new CsvWriter(stringWriter, CultureInfo.InvariantCulture))
        //    {
        //        // Write the CSV header using the keys from the first JSON object

        //        var headers = ((JObject)jsonArray.First).Properties().Select(p => p.Name);

        //        string[] stringArray = { "CustomerReference", "LoadId", "ItemCode", "ItemQuantity", "ItemWeight", "Street", "City", "State", "PostalCode", "Country" };

        //        foreach (var header in stringArray)
        //        {
        //            csvWriter.WriteField(header);
        //        }
        //        csvWriter.NextRecord();

        //        // Write the rows
        //        foreach (var jsonObj in jsonArray)
        //        {
        //            foreach (var header in headers)
        //            {
        //                csvWriter.WriteField(jsonObj[header]?.ToString());
        //            }
        //            csvWriter.NextRecord();
        //        }

        //        return stringWriter.ToString();
        //    }
        //}

        public static void UploadCsvToSftp(string csvData, string fileName)
        {
            // SFTP credentials from environment variables
            string host = "ap-southeast-1.sftpcloud.io"; //Environment.GetEnvironmentVariable("ap-southeast-1.sftpcloud.io");
            int port = 22; //int.Parse(Environment.GetEnvironmentVariable("SftpPort") ?? "22");
            string username = "BlueCorp01"; //Environment.GetEnvironmentVariable("SftpUsername");
            string password = "8TlYt77Pe9fxpxvWr70DZioPmDs4J8rq"; //Environment.GetEnvironmentVariable("SftpPassword");

            using (var client = new SftpClient(host, port, username, password))
            {
                client.Connect();

                // Convert CSV content to a byte stream
                using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(csvData)))
                {
                    client.UploadFile(memoryStream, fileName);
                }

                client.Disconnect();
            }
        }

        public static void JsonToStorageAccountAsync(string jsonString, string fileName) 
        {
            string connectionString = "DefaultEndpointsProtocol=https;AccountName=stmohaappdev;AccountKey=KBLyIBXy4t1yDr3y3jAKxFVL6i7REkYN5h4MyOuGBY4vA+as6w//fpQebsU9Ob8+c6lL2aoGMJQE+AStPLi7Ig==;EndpointSuffix=core.windows.net";
            string containerName = "bluecorp-incoming"; // Replace with your container name
            string blobName = $"data-{DateTime.UtcNow:yyyyMMddHHmmss}.json"; // Unique file name

            // Create the Blob client
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Ensure the container exists
            containerClient.CreateIfNotExistsAsync();

            // Get a BlobClient for the new blob
            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            // Upload the JSON string to the blob
            using (var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonString)))
            {
                blobClient.UploadAsync(stream, overwrite: true);
            }
        }

        public static DataTable JsonStringToTable(string jsonContent)
        {
            DataTable dt = JsonConvert.DeserializeObject<DataTable>(jsonContent);
            return dt;
        }

        public static string MapPath(string relativePath)
        {
            string rootPath = System.IO.Directory.GetCurrentDirectory();
            return System.IO.Path.Combine(rootPath, relativePath.TrimStart('/'));
        }

        //public static void ConvertJsonToCsv(List<Dictionary<string, object>> data, string csvFilePath)
        //{
        //    // Create a StringBuilder for the CSV content
        //    StringBuilder csvContent = new StringBuilder();

        //    // Extract and write the header row (keys of the first dictionary)
        //    var headers = data[0].Keys;
        //    csvContent.AppendLine(string.Join(",", headers));

        //    // Write the data rows
        //    foreach (var record in data)
        //    {
        //        var values = new List<string>();
        //        foreach (var key in headers)
        //        {
        //            // Handle null values and escape commas
        //            var value = record.ContainsKey(key) && record[key] != null ? record[key].ToString() : string.Empty;
        //            values.Add(EscapeCsvValue(value));
        //        }
        //        csvContent.AppendLine(string.Join(",", values));
        //    }

        //    // Write the CSV content to the file
        //    File.WriteAllText(csvFilePath, csvContent.ToString());
        //}

        public static string EscapeCsvValue(string value)
        {
            // Escape double quotes and surround the value with quotes if it contains commas or quotes
            if (value.Contains(",") || value.Contains("\""))
            {
                value = value.Replace("\"", "\"\"");
                value = $"\"{value}\"";
            }
            return value;
        }
    }
}
