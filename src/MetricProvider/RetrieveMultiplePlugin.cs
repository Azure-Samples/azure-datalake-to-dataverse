using MetricProvider.interfaces;
using MetricProvider.models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetricProvider
{
    public class RetrieveMultiplePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.Get<IPluginExecutionContext>();
            EntityCollection collection = new EntityCollection();

            string aadTenantId = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_AADTenant");
            string aadClientId = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_AADClientId");
            string aadClientSecret = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_AADClientSecret");
            string aadScope = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_AADTokenScope");
            string path = Connection.GetEnvironmentVariable(serviceProvider, $"{Connection.PublisherName}_DataPath");

            string token = Connection.GetToken(aadTenantId, aadClientId, aadClientSecret, aadScope).Result;

            IHandler reader = new CSVHandler();
            ReadResult rr = reader.Read(path, token).Result;

            foreach (List<string> value in rr.values)
            {
                Entity e = new Entity("new_metric");
                for (int i = 0; i < rr.keys.Count; i++)
                {
                    Guid guid;
                    Decimal dec;
                    if (Guid.TryParse(value[i], out guid))
                    {
                        e.Attributes.Add("new_" + rr.keys[i].ToLower(), guid);
                    }
                    else if (Decimal.TryParse(value[i], out dec))
                    {
                        e.Attributes.Add("new_" + rr.keys[i].ToLower(), dec);
                    }
                    else
                    {
                        e.Attributes.Add("new_" + rr.keys[i].ToLower(), value[i]);
                    }
                }
                collection.Entities.Add(e);
            }

            context.OutputParameters["BusinessEntityCollection"] = collection;

        }
    }
}
