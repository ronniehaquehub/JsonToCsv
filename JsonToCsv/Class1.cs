using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

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

        public static SecretClient GetSecretClient()
        {
            var tenantId = Environment.GetEnvironmentVariable("TenantId");
            var clientId = Environment.GetEnvironmentVariable("ClientId");
            var clientSecret = Environment.GetEnvironmentVariable("ClientSecret");
            var vaultUri = new Uri(Environment.GetEnvironmentVariable("KeyVaultUri"));

            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            var secretClient = new SecretClient(vaultUri, credential);

            return secretClient;
        }

        public static void UploadCsvToSftp(string csvData, string fileName)
        {
            var secretClient = GetSecretClient();

            string sftpHostSecretName = Environment.GetEnvironmentVariable("SFTPHostSecretName"); //"3PL-SFTP-host";
            string sftpUsernameSecretName = Environment.GetEnvironmentVariable("SFTPUsernameSecretName"); //"3PL-SFTP-username"; 
            string sftpPasswordSecretName = Environment.GetEnvironmentVariable("SFTPPasswordSecretName");

            string sftpHost = secretClient.GetSecret(sftpHostSecretName).Value.Value.ToString();
            string sftpUser = secretClient.GetSecret(sftpUsernameSecretName).Value.Value.ToString();
            string sftpPass = secretClient.GetSecret(sftpPasswordSecretName).Value.Value.ToString();
            int port = 22;

            using (var client = new SftpClient(sftpHost, port, sftpUser, sftpPass))
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
    }
}
