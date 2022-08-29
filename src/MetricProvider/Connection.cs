using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MetricProvider
{
    public class Connection
    {
        public static string PublisherName = "<REPLACE WITH YOUR PUBLISHER NAME>";
        public static async Task<string> GetToken(string tenant, string clientId, string clientSecret, string scope)
        {
            HttpClient httpClient = new HttpClient();

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("scope", scope)
            });

            var response = await httpClient.PostAsync($"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token", formContent);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed when retrieving OAuth token for Data lake");
            }
            string res = await response.Content.ReadAsStringAsync();
            int tokenStart = res.IndexOf("\"access_token\":");
            int tokenEnd = res.IndexOf("\"", tokenStart + 16);
            string token = res.Substring(tokenStart + 16, tokenEnd - tokenStart - 16);
            return token;
        }


        public static string GetEnvironmentVariable(IServiceProvider serviceProvider, string logicalName)
        {
            var PluginExecutionContext = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(PluginExecutionContext.UserId);

            OrganizationRequest request = new OrganizationRequest("RetrieveEnvironmentVariableValue");
            request.Parameters["DefinitionSchemaName"] = logicalName;

            var response = service.Execute(request);

            var secretvalue = response.Results["Value"];

            return (string)secretvalue;
        }

    }
}
