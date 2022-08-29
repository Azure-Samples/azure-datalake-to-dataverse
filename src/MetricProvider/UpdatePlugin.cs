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
    public class UpdatePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.Get<IPluginExecutionContext>();
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                Entity entity = (Entity)context.InputParameters["Target"];

                string aadTenantId = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_AADTenant");
                string aadClientId = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_AADClientId");
                string aadClientSecret = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_AADClientSecret");
                string aadScope = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_AADTokenScope");
                string path = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_DataPath");

                string token = Connection.GetToken(aadTenantId, aadClientId, aadClientSecret, aadScope).Result;

                IHandler handler = new CSVHandler();

                List<string> elements = new List<string>();
                try
                {
                    elements.Add(entity["new_metricid"].ToString());
                    if(entity.Contains("new_name"))
                    {
                        elements.Add(entity["new_name"].ToString());
                    }
                    else
                    {
                        elements.Add(null);
                    }
                    if(entity.Contains("new_value"))
                    {
                        elements.Add(entity["new_value"].ToString());
                    }
                    else
                    {
                        elements.Add(null);
                    }
                    handler.Update(path, new Guid(entity["new_metricid"].ToString()), elements, token).Wait();
                }
                catch (Exception e)
                {
                    throw new InvalidPluginExecutionException(e.Message);
                }

            }
        }
    }
}
