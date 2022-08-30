# Azure Data Lake to Dataverse

This sample demonstrates how an Azure Data Lake file can be synced to a Dataverse table. The main use case for this scenario is bringing aggregated data created by a big data platform (e.g. Azure Synapse) and stored in a storage account or data lake back to the user application landscape (e.g. Power Apps) by leveraging virtual tables. This is particularely useful when no dedicated SQL database can or should be used.

<img src="assets/header.png" width="300">

## Prerequisites

The following requirements must be met in order to follow the steps below

- Access to an Azure subscription
- Visual Studio IDE
- .NET Framework 4.7.1 must be installed
- Access to Power Platform (and sufficient permissions to create solutions and tables)
- Python 3 installed (for helper scripts)

## Creating a synced virtual table

### 1. Service Principal creation

The service principal created in this step will be used by the Dynamics custom virtual table provider to access the data lake as well as the helper scripts to create a virtual table in Power Platform.

a. Login to `portal.azure.com` and switch to "Azure Active Directory" > "App registration" > "New registration". Add a name and click "Register.

b. In the newly created app in the "Overview" tab note down the "Application (client) ID" and the "Directory (tenant) ID". Then switch to "Certificates & secrets", create a new client secret and note it down as well.

c. Switch to "API permissions" > "Add a permission" > "Dynamics CRM" > select "user_impersonation" and click "Add permissions". The permission page should now look like the following:

<img src="assets/aad-sp.png" width="800">

### 2. Azure Data Lake & sample data

If you already have a data lake in place you can skip section a) and continue with b).

a. Go to `portal.azure.com`, click on "Create a resource", type and select "Storage account". Select the appropriate name and configurations, make sure that you set the option "Enable hierarchical namespace" in the Advanced option. Review and create the storage account. After creation finished, go to "Containers" and add a container with a name of your choice.

<img src="assets/datalake_creation.png" width="500">

b. In your storage account, go to "Access Control (IAM)" and click on "Add" > "Add role assignment". Type and select "Storage Account Contributor" > Next > "+ Select Members" and type the name of the app you created in step 1. This will allow the service principal to access the storage account and read and write data.

c. We now add sample data to the storage account by using the helper script. NOTE: Don't use the Azure Portal to add sample data, otherwise the plugin tool will fail to write or update this file later on. Open a command prompt/shell, change directory to `scripts/` and execute the following command after replacing the dummy values:

```
python helper.py datalake sampledata --aad_tenant <AAD tenant name> --aad_client_id <AAD app registration client ID> --aad_client_secret <AAD client secret> --datalake_name <name of your datalake> --datalake_container_name <name of data lake container>
```

This will create a file metrics.csv in the root folder of your container with the following content.

```
MetricId,Name,Value
"bbb9792d-9fbf-45d5-88e5-dce2acd4924c","AverageTripDuration",26.1
"1cb4e68d-6ee3-4b1b-b90b-a1e49daeef03","LongestTrip",180.5
"262bd819-8eaa-44c8-96f8-eced6874cba1","WeekendWeekdayRatio",0.45
```

If the script raises an exception, check if the values are correct and if your service principal has "Storage Blob Data Contributor" permissions on the data lake.


### 3. Adopting the virtual table provider code

a. Log into your Power Platform environment and click on "Solutions" > "New solution". Select a name and a publisher and click on "Create". In your solution, click on "New" > "More" > "Environment Variable". We will add the following environment variables:

| Name                          | Data Type       | Value |
| ----------------------------- | --------------- | ----- |
| \<publisher\>_AADTenant       | Text            | The AAD tenant ID |
| \<publisher\>_AADClientId     | Text            | The app registration client Id
| \<publisher\>_AADClientSecret | Text            | The app registration client secret
| \<publisher\>_AADTokenScope   | Text            | https://\<datalakename\>.dfs.core.windows.net/.default
| \<publisher\>_DataPath        | Text            | https://\<datalakename\>.dfs.core.windows.net/\<containername\>/metrics.csv

NOTE: If you have an Azure Key Vault available, a good practice is to store the client secret there and use the Data Type "Secret" instead of plain text. In code the secret can then be retrieved by placing a `new OrganizationRequest("RetrieveEnvironmentVariableSecretValue");`

b. Open the `MetricProvider.sln` file under `src/` and switch to `Connection.cs`. Find the line `public static string PublisherName = "<REPLACE WITH YOUR PUBLISHER NAME>";` and replace the value with your publisher name.

c. In the Visual Studio Solution Explorer right click on "MetricProvider" > "Properties" > "Signing" and click on "Sign the assembly". Select an existing key or create a new one. Note, that the assembly must be signed, otherwise the subsequent steps will fail.

<img src="assets/signing.png" width="500">

d. Right click on "Metric Provider" > "Build". This will create a .dll file in the `bin/` folder.

### 4. Registering the assembly & data providers

a. We use the Plugin Registration tool to upload the .dll we created in the previous step to Power Platform. Therefore, [download the tool](https://www.nuget.org/packages/Microsoft.CrmSdk.XrmTooling.PluginRegistrationTool) switch to the package download location and open `PluginRegistration.exe`.

b. Click on "Create new connection" and enter your Power Platform login credentials. Then click on "Register" > "Register new assembly"

<img src="assets/assembly_registration.png" width="500">

Select your MetricProvider.dll binary and click on "Register Selected Plugin".

c. Click on "Register" > "Register New Data Provider". Choose the name "MetricDP" and select the Power Platform solution you created earlier. Under "Data Source Entity" select "Create New Data Source"

<img src="assets/datasource.png" width="500">

Enter "MetricDS" as the display name, add your solution and click "Create" - this will create a virtual table entity in your Dataverse.

d. Go back to Power Platform and click on "Settings" > "Advanced settings" > "Administration" > "Virtual Entity Data Sources" > "New". Select the data provider "MetricDP" you created earlier and enter the new name "MetricDatasource" for the virtual entity data source.

<img src="assets/datasource_final.png" width="500">

### Virtual table creation & testing

a. We have to add the service principal we created in the first step to the Power Platform environment. Go to the Power Platform Admin center > "Environments" > Select your environment > "Settings" > "Users + permissions" > "Application users". Click on "New app user" and select the app registration you created earlier. 

b. Select the newly created user and click "Edit security roles", enable the "Dataverse Search Role" and "System Administrator" (after creation of the virtual table you can remove these permissions again).

c. The final step is creating the actual virtual table. For doing so we make use of the helper script again. Switch to the scripts/ folder and run the following command:

```
python helper.py dataverse virtualtable --aad_tenant <AAD tenant name> --aad_client_id <AAD app registration client ID> --aad_client_secret <AAD client secret> --power_apps_org <Power Platform environment name> --publisher <Power Platform publisher name> --provider_name MetricDP --datasource_name MetricDatasource
```

This should create a new table "Metric" in Dataverse. We should now be able to see the content of the metric.csv file from Azure Data Lake in dataverse.

<img src="assets/tabledata.png" width="800">

NOTE: The plugin presented in this repository is reading and parsing a CSV file from Data Lake using the REST API. This has certain performance limitations and is therefore only recommended for small datasets.

## Troubleshooting

If you encounter issues while adopting and developing the custom virtual table provider, you might use the Power Platform trace log by clicking on settings > "Advanced settings" > "Plug-In Trace Log". Whenever a `InvalidPluginExecutionException` exception is thrown, this will be visible here.

<img src="assets/plugintracelog.png" width="500">


## Resources

- Using custom virtual table providers with a SQL database backend instead of a data lake file: https://docs.microsoft.com/en-us/power-apps/developer/data-platform/virtual-entities/sample-ve-provider-crud-operations
- Dataverse REST API: https://docs.microsoft.com/en-us/power-apps/developer/data-platform/webapi/associate-disassociate-entities-using-web-api