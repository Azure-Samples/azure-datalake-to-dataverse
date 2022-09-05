using MetricProvider.interfaces;
using MetricProvider.models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace MetricProvider
{
    public class CSVHandler : IHandler
    {
        private bool skipHeader;
        private readonly char delimiter;
        private readonly HttpClient httpClient;
        public CSVHandler(bool skipHeader = false, char delimiter = ',')
        {
            this.skipHeader = skipHeader;
            this.delimiter = delimiter;
            httpClient = new HttpClient();
        }

        private string ReadResultToCSV(ReadResult res)
        {
            string content = "";
            foreach(string key in res.keys)
            {
                content += key + delimiter.ToString();
            }
            content = content.Substring(0, content.LastIndexOf(delimiter.ToString())) + "\n";

            foreach (List<string> value in res.values)
            {
                for (int i = 0; i < res.keys.Count; i++)
                {
                    if (decimal.TryParse(value[i], out decimal dec))
                    {
                        content += value[i] + delimiter;
                    }
                    else
                    {
                        content += "\"" + value[i] + "\"" + delimiter;
                    }
                }
                content = content.Substring(0, content.LastIndexOf(delimiter.ToString())) + "\n";
            }

            return content;
        }

        private async Task CreateFile(string path, string content, string accessToken)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response = await httpClient.PutAsync(path + "?resource=file&position=0", null);

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                throw new Exception(response.ReasonPhrase);
            }

            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), path + $"?action=append&position=0")
            {
                Content = new StringContent(content)
            };
            response = await httpClient.SendAsync(request);

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                throw new Exception(response.ReasonPhrase);
            }

            int contentLength = content.Length;
            request = new HttpRequestMessage(new HttpMethod("PATCH"), path + $"?action=flush&position={contentLength}");
            response = await httpClient.SendAsync(request);

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                throw new Exception(response.ReasonPhrase);
            }

        }

        public async Task Update(string path, Guid id, IList<string> elements, string accessToken)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            ReadResult res = await Read(path, accessToken);

            foreach (List<string> value in res.values)
            {
                bool updateRow = false;
                for (int i = 0; i < res.keys.Count; i++)
                {
                    if (i == 0)
                    {
                        if(Guid.Parse(value[i]) == id)
                        {
                            updateRow = true;
                        }
                    }
                    else
                    {
                        if(updateRow && elements[i] != null)
                        {
                            value[i] = elements[i];
                        }
                    }
                }

            }

            string content = ReadResultToCSV(res);
            await CreateFile(path, content, accessToken);

        }
        public async Task Delete(string path, Guid id, string accessToken)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            ReadResult res = await Read(path, accessToken);

            for (int j = res.values.Count - 1; j >= 0; j--)
            {
                for (int i = 0; i < res.keys.Count; i++)
                {
                    if (i == 0)
                    {
                        if (Guid.Parse(res.values[j][i]) == id)
                        {
                            res.values.RemoveAt(j);
                            break;
                        }
                    }
                }
            }

            string content = ReadResultToCSV(res);
            await CreateFile(path, content, accessToken);
        }

        public async Task Write(string path, IList<string> elements, string accessToken)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            int fileLength = await GetFileLength(path, accessToken);

            string row = "";

            foreach(string element in elements)
            {
                if (decimal.TryParse(element, out decimal dec))
                {
                    row += element + delimiter;
                }
                else
                {
                    row += "\"" + element + "\"" + delimiter;
                }
            }
            row = row.Substring(0, row.LastIndexOf(delimiter.ToString())) + "\n";

            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), path + $"?action=append&position={fileLength}")
            {
                Content = new StringContent(row)
            };
            HttpResponseMessage response = await httpClient.SendAsync(request);

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                throw new Exception(response.ReasonPhrase);
            }

            int newLength = row.Length + fileLength;
            request = new HttpRequestMessage(new HttpMethod("PATCH"), path + $"?action=flush&position={newLength}");
            response = await httpClient.SendAsync(request);

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                throw new Exception(response.ReasonPhrase);
            }

        }

        public async Task<int> GetFileLength(string path, string accessToken)
        {
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response = await httpClient.GetAsync(path);
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                throw new Exception(response.ReasonPhrase);
            }
            string content = await response.Content.ReadAsStringAsync();
            return content.Length;
        }

        public async Task<ReadResult> Read(string path, string accessToken)
        {
            IList<List<string>> values = new List<List<string>>();
            IList<string> keys = new List<string>();
            ReadResult result = new ReadResult();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            HttpResponseMessage response = await httpClient.GetAsync(path);
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                throw new Exception(response.ReasonPhrase);
            }
            string content = await response.Content.ReadAsStringAsync();

            foreach (string line in content.Split(Environment.NewLine.ToCharArray()))
            {
                if (line == "")
                {
                    continue;
                }
                string[] val = line.Replace("\"", "").Split(delimiter);
                if (!skipHeader)
                {
                    foreach (string value in val)
                    {
                        keys.Add(value);
                    }
                    skipHeader = true;
                }
                else
                {
                    List<string> row = new List<string>(val);
                    values.Add(row);
                }
            }

            result.keys = keys;
            result.values = values;
            return result;

        }
    }
}
