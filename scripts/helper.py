from importlib.metadata import files
from azure.identity import AzureCliCredential
import sys
import os
import json
import requests
import argparse
from typing import List
from urllib.parse import urljoin
from functools import reduce
import base64
import json


class AzureDataLakeHandler:
    """
    Class to handle access to Azure Data Lake. Primarely used to create files in a set data lake filesystem
    """

    def __init__(self, data_lake_name, tenant=None, client_id=None, client_secret=None):
        self._base_url = f"https://{data_lake_name}.dfs.core.windows.net/"
        self._scope = self._base_url + ".default"

        if client_secret is None:
            credentials = self._get_credentials_cli(self._scope)
            self._tenant = credentials["tenant"]
            self._client_id = credentials["client_id"]
            self._token = credentials["token"]
        else:
            self._tenant = tenant
            self._client_id = client_id
            self._client_secret = client_secret
            self._token = self._get_token(self._scope)

        self._headers = {
            "Content-Type": "application/json",
            "Authorization": f"Bearer {self._token}",
        }

    def _get_credentials_cli(self, scope) -> str:
        """
        Helper function to get a bearer token using the active Azure CLI login session
        """
        credential = AzureCliCredential()
        res = credential.get_token(scope)
        parsed_token = base64.b64decode(
            res.token.split(".")[1] + "=" * (-len(res.token.split(".")[1]) % 4)
        )
        payload = json.loads(parsed_token)
        tenant_parts = payload["iss"].split("/")
        tenant = tenant_parts[len(tenant_parts) - 2]
        credentials = {
            "tenant": tenant,
            "client_id": payload["appid"],
            "token": res.token,
        }
        return credentials

    def _get_token(self, scope) -> str:
        """
        Helper function to get a bearer token when a client ID and client secret were passed
        """
        token_url = (
            f"https://login.microsoftonline.com/{self._tenant}/oauth2/v2.0/token"
        )

        post_data = {
            "grant_type": "client_credentials",
            "client_id": self._client_id,
            "client_secret": self._client_secret,
            "scope": scope,
        }

        res = requests.post(token_url, data=post_data)
        if res.status_code != 200:
            raise Exception(res)
        data = res.json()
        return data["access_token"]

    def create_basic_metrics_file(
        self, filesystem, path="/", filename="metrics.csv"
    ) -> None:
        base_url = self._base_url + filesystem + path + filename
        create_url = base_url + "?resource=file&position=0"
        res = requests.put(
            create_url, headers=self._headers
        )  # First an empty file is created in data lake

        if int(res.status_code / 100) != 2:
            raise Exception(res.text)

        content = 'MetricId,Name,Value\n"bbb9792d-9fbf-45d5-88e5-dce2acd4924c","AverageTripDuration",26.1\n"1cb4e68d-6ee3-4b1b-b90b-a1e49daeef03","LongestTrip",180.5\n"262bd819-8eaa-44c8-96f8-eced6874cba1","WeekendWeekdayRatio",0.45\n'

        patch_url = base_url + "?action=append&position=0"
        res = requests.patch(
            patch_url, data=content, headers=self._headers
        )  # Here a file is set ready to be appended - it is not actually appended before the flush action is called

        if int(res.status_code / 100) != 2:
            raise Exception(res.text)

        content_length = len(content)
        flush_url = base_url + f"?action=flush&position={content_length}"
        res = requests.patch(
            flush_url, headers=self._headers
        )  # Finising the file append process by flushing to the file

        if int(res.status_code / 100) != 2:
            raise Exception(res.text)


class DataverseHandler:
    """
    Class to manage access to Power Platform and Dataverse.
    """

    def __init__(
        self,
        power_apps_org,
        publisher,
        tenant=None,
        client_id=None,
        client_secret=None,
        schema_folder_path=None,
    ):
        """
        Helps to create Dataverse objects like choices, tables, relationships
        """
        self.power_apps_org = power_apps_org
        self.scope = f"https://{power_apps_org}.api.crm4.dynamics.com/.default"
        self.base_url = (
            f"https://{self.power_apps_org}.api.crm4.dynamics.com/api/data/v9.2/"
        )
        self.pluginassemblies_url = urljoin(self.base_url, "pluginassemblies")
        self.plugintypes_url = urljoin(self.base_url, "plugintypes")
        self.providers_url = urljoin(self.base_url, "entitydataproviders")
        self.datasource_url = urljoin(self.base_url, "entitydatasources")
        self.entity_definitions_url = urljoin(self.base_url, "EntityDefinitions")
        self.publisher = publisher
        self.verify_ssl = True
        if schema_folder_path is None:
            self.schema_folder_path = os.path.join(os.path.dirname(__file__), "schemas")
        else:
            self.schema_folder_path = schema_folder_path

        if client_secret is None:
            credentials = self._get_credentials_cli(self.scope)
            self.tenant = credentials["tenant"]
            self.client_id = credentials["client_id"]
            self._token = credentials["token"]
        else:
            self.tenant = tenant
            self.client_id = client_id
            self.client_secret = client_secret
            self._token = self._get_token(self.scope)

        self.is_running = False
        self.batch_size = 10

    def _get_credentials_cli(self, scope) -> str:
        """
        Helper function to get a bearer token using the active Azure CLI login session
        """
        credential = AzureCliCredential()
        res = credential.get_token(scope)
        parsed_token = base64.b64decode(
            res.token.split(".")[1] + "=" * (-len(res.token.split(".")[1]) % 4)
        )
        payload = json.loads(parsed_token)
        tenant_parts = payload["iss"].split("/")
        tenant = tenant_parts[len(tenant_parts) - 2]
        credentials = {
            "tenant": tenant,
            "client_id": payload["appid"],
            "token": res.token,
        }
        return credentials

    def _get_token(self, scope) -> str:
        """
        Helper function to get a bearer token when a client ID and client secret were passed
        """
        token_url = f"https://login.microsoftonline.com/{self.tenant}/oauth2/v2.0/token"

        post_data = {
            "grant_type": "client_credentials",
            "client_id": self.client_id,
            "client_secret": self.client_secret,
            "scope": scope,
        }

        res = requests.post(token_url, data=post_data, verify=self.verify_ssl)
        if res.status_code != 200:
            raise Exception(res)
        data = res.json()
        return data["access_token"]

    def _change_schema(self, schema, change_function) -> None:
        """
        This method can be used to change the schema, for example
        by injecting prefixes to certain attributes
        """
        for key, item in schema.items():
            if type(schema[key]) is dict:
                self._change_schema(schema[key], change_function)
            elif type(schema[key]) is list:
                for el in schema[key]:
                    self._change_schema(el, change_function)
            else:
                change_function(key, schema)

    def _load_definitions(self, folder_path, schema_file_name=None) -> List[dict]:
        """
        Loads local JSON files that represent Power Apps objects (Dataverse tables, Choices)
        """
        schemas = []
        for path, subdirs, files in os.walk(folder_path):
            for name in files:
                if not (schema_file_name is not None and name != schema_file_name):
                    filename = os.path.join(path, name)
                    with open(filename, "r") as f:
                        schema = json.loads(f.read())
                        schemas.append((name, schema))
        return schemas

    def create_virtual_table(self, provider_uuid, datasource_uuid) -> None:
        """
        Creates a virtual table in Dataverse based on a JSON definition file
        """
        filename, schema = self._load_definitions(
            self.schema_folder_path, "virtualtable.json"
        )[0]

        headers = {
            "Content-Type": "application/json",
            "Authorization": f"Bearer {self._token}",
        }

        def changes(key, value):
            if key == "DataProviderId" and "<GUID>" in value[key]:
                value[key] = value[key].replace("<GUID>", provider_uuid)
            elif key == "DataSourceId" and "<GUID>" in value[key]:
                value[key] = value[key].replace("<GUID>", datasource_uuid)

        self._change_schema(schema, changes)

        res = requests.post(self.entity_definitions_url, json=schema, headers=headers)
        print(f"{res.status_code} for {schema['SchemaName']}")
        if int(res.status_code / 100) != 2:
            print(res.text)

    def get_entitydatasourceid_by_name(self, datasource_name) -> str:
        """
        Returns the entitydatasourceid property of Dynamics based on the name of the data source
        """
        headers = {
            "Content-Type": "application/json",
            "Authorization": f"Bearer {self._token}",
        }

        res = requests.get(self.datasource_url, headers=headers)
        datasources = res.json()

        datasource_id = ""
        for datasource in datasources["value"]:
            name = datasource["name"]
            if name == datasource_name:
                datasource_id = datasource["entitydatasourceid"]
                break

        return datasource_id

    def get_entitydataproviderid_by_name(self, provider_name) -> str:
        """
        Returns the entitydataproviderid property of Dynamics based on the name of the data provider
        """
        headers = {
            "Content-Type": "application/json",
            "Authorization": f"Bearer {self._token}",
        }
        res = requests.get(self.providers_url, headers=headers)

        if int(res.status_code / 100) != 2:
            raise Exception(res.text)

        providers = res.json()

        provider_id = ""
        for provider in providers["value"]:
            name = provider["name"]
            if name == provider_name:
                provider_id = provider["entitydataproviderid"]
                break

        return provider_id


if __name__ == "__main__":
    parser = argparse.ArgumentParser()

    subparsers = parser.add_subparsers(help="Manage Dataverse objects programmatically")

    datalake_parser = subparsers.add_parser("datalake")
    dataverse_parser = subparsers.add_parser("dataverse")

    datalake_parser.add_argument("type", choices=["sampledata"])
    datalake_parser.add_argument("--aad_tenant", "-t", type=str, required=False)
    datalake_parser.add_argument("--aad_client_id", "-i", type=str, required=False)
    datalake_parser.add_argument("--aad_client_secret", "-s", type=str, required=False)
    datalake_parser.add_argument("--datalake_name", "-d", type=str, required=True)
    datalake_parser.add_argument(
        "--datalake_container_name", "-c", type=str, required=True
    )

    dataverse_parser.add_argument("type", choices=["virtualtable"])
    dataverse_parser.add_argument("--aad_tenant", "-t", type=str, required=False)
    dataverse_parser.add_argument("--aad_client_id", "-i", type=str, required=False)
    dataverse_parser.add_argument("--aad_client_secret", "-s", type=str, required=False)
    dataverse_parser.add_argument("--power_apps_org", "-o", type=str, required=True)
    dataverse_parser.add_argument(
        "--publisher",
        "-p",
        type=str,
        required=True,
        help="Publisher used as a prefix for the table",
    )
    dataverse_parser.add_argument("--provider_name", "-r", type=str, required=False)
    dataverse_parser.add_argument("--datasource_name", "-d", type=str, required=False)

    args = parser.parse_args()

    if sys.argv[1] == "datalake":
        adlh = AzureDataLakeHandler(
            args.datalake_name,
            args.aad_tenant,
            args.aad_client_id,
            args.aad_client_secret,
        )
        if args.type == "sampledata":
            adlh.create_basic_metrics_file(args.datalake_container_name)
        else:
            print(f"Invalid command {sys.argv[1]}")
    elif sys.argv[1] == "dataverse":
        dvf = DataverseHandler(
            args.power_apps_org,
            args.publisher,
            args.aad_tenant,
            args.aad_client_id,
            args.aad_client_secret,
        )
        if args.type == "virtualtable":
            provider_uuid = dvf.get_entitydataproviderid_by_name(args.provider_name)
            datasource_uuid = dvf.get_entitydatasourceid_by_name(args.datasource_name)
            dvf.create_virtual_table(provider_uuid, datasource_uuid)
        else:
            print(f"Invalid command {sys.argv[1]}")
