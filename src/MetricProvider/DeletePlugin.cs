using MetricProvider.interfaces;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetricProvider
{
    public class DeletePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.Get<IPluginExecutionContext>();

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is EntityReference)
            {
                EntityReference entityRef = (EntityReference)context.InputParameters["Target"];
                Guid id = entityRef.Id;

                string aadTenantId = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_AADTenant");
                string aadClientId = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_AADClientId");
                string aadClientSecret = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_AADClientSecret");
                string aadScope = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_AADTokenScope");
                string path = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_DataPath");

                string token = Connection.GetToken(aadTenantId, aadClientId, aadClientSecret, aadScope).Result;

                IHandler handler = new CSVHandler();
                handler.Delete(path, id, token).Wait();
            }
        }
    }
}
