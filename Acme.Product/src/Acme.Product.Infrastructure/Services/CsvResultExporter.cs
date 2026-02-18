// CsvResultExporter.cs
// CsvResultExporter实现
// 作者：蘅芜君

using System.Globalization;
using Acme.Product.Application.DTOs;
using Acme.Product.Application.Services;
using CsvHelper;

namespace Acme.Product.Infrastructure.Services;

public class CsvResultExporter : IResultExporter
{
    public async Task<byte[]> ExportToCsvAsync(List<InspectionResultDto> results)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Customize mapping if needed, or rely on AutoMap
        // For Defects list, we might want to flatten or ignore
        // csv.Context.RegisterClassMap<InspectionResultMap>();
        
        await csv.WriteRecordsAsync(results);
        await writer.FlushAsync();
        
        return memoryStream.ToArray();
    }
}