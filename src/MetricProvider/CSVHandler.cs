using MetricProvider.interfaces;
using MetricProvider.models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace MetricProvider
{
    public class CSVHandler : IHandler
    {
        private bool skipHeader;
        private char delimiter;
        private HttpClient httpClient;
        public CSVHandler(bool skipHeader = false, char delimiter = ',')
        {
            this.skipHeader = skipHeader;
            this.delimiter = delimiter;
            this.httpClient = new HttpClient();
        }

        private string ReadResultToCSV(ReadResult res)
        {
            string content = "";
            foreach(string key in res.keys)
            {
                content += key + this.delimiter.ToString();
            }
            content = content.Substring(0, content.LastIndexOf(this.delimiter.ToString())) + "\n";

            foreach (List<string> value in res.values)
            {
                for (int i = 0; i < res.keys.Count; i++)
                {
                    Decimal dec;
                    if (Decimal.TryParse(value[i], out dec))
                    {
                        content += value[i] + this.delimiter;
                    }
                    else
                    {
                        content += "\"" + value[i] + "\"" + this.delimiter;
                    }
                }
                content = content.Substring(0, content.LastIndexOf(this.delimiter.ToString())) + "\n";
            }

            return content;
        }

        private async Task CreateFile(string path, string content, string accessToken)
        {
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await this.httpClient.PutAsync(path + "?resource=file&position=0", null);

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                throw new Exception(response.ReasonPhrase);
            }

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), path + $"?action=append&position=0");
            request.Content = new StringContent(content);
            response = await this.httpClient.SendAsync(request);

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
            response = await this.httpClient.SendAsync(request);

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                throw new Exception(response.ReasonPhrase);
            }

        }

        public async Task Update(string path, Guid id, List<string> elements, string accessToken)
        {
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
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
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
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

        public async Task Write(string path, List<string> elements, string accessToken)
        {
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            int fileLength = await GetFileLength(path, accessToken);

            string row = "";

            foreach(string element in elements)
            {
                Decimal dec;
                if (Decimal.TryParse(element, out dec))
                {
                    row += element + this.delimiter;
                }
                else
                {
                    row += "\"" + element + "\"" + this.delimiter;
                }
            }
            row = row.Substring(0, row.LastIndexOf(this.delimiter.ToString())) + "\n";

            var request = new HttpRequestMessage(new HttpMethod("PATCH"), path + $"?action=append&position={fileLength}");
            request.Content = new StringContent(row);
            var response = await this.httpClient.SendAsync(request);

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
            response = await this.httpClient.SendAsync(request);

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
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync(path);
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
            List<List<string>> values = new List<List<string>>();
            List<string> keys = new List<string>();
            ReadResult result = new ReadResult();
            this.httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync(path);
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
                var val = line.Replace("\"", "").Split(delimiter);
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
