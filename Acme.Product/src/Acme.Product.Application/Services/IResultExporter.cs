// IResultExporter.cs
// ResultExporter接口定义
// 作者：蘅芜君

using Acme.Product.Application.DTOs;

namespace Acme.Product.Application.Services;

public interface IResultExporter
{
    Task<byte[]> ExportToCsvAsync(List<InspectionResultDto> results);
}