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
    public class RetrievePlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.Get<IPluginExecutionContext>();

            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is EntityReference)
            {
                EntityReference entityRef = (EntityReference)context.InputParameters["Target"];
                Entity e = new Entity("new_metric");

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
                    for (int i = 0; i < rr.keys.Count; i++)
                    {
                        if (entityRef.Id == Guid.Parse(value[0]))
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
                    }
                }

                context.OutputParameters["BusinessEntity"] = e;

            }

        }
    }
}
