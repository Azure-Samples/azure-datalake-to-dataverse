using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MetricProvider.models;

namespace MetricProvider.interfaces
{
    public interface IHandler
    {
        Task<ReadResult> Read(string filePath, string accessToken);
        Task Write(string filePath, List<string> elements, string accessToken);
        Task Update(string filePath, Guid id, List<string> elements, string accessToken);
        Task Delete(string filePath, Guid id, string accessToken);
    }
}
