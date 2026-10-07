using ClosedXML.Excel;
using ZupuPaiBan.Models;

namespace ZupuPaiBan.Services;

public class ExcelService
{
    public async Task<List<FamilyMember>> ImportFromExcelAsync(string filePath)
    {
        var members = new List<FamilyMember>();
        
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.First();
        var rows = worksheet.RowsUsed().Skip(1); // Skip header row

        int id = 1;
        foreach (var row in rows)
        {
            var member = new FamilyMember
            {
                Id = id++,
                Name = row.Cell(1).GetValue<string>() ?? "",
                Gender = row.Cell(2).GetValue<string>() == "女" ? Gender.Female : Gender.Male,
                BirthDate = row.Cell(3).GetValue<string>() ?? "",
                DeathDate = string.IsNullOrEmpty(row.Cell(4).GetValue<string>()) ? null : row.Cell(4).GetValue<string>(),
                SpouseName = string.IsNullOrEmpty(row.Cell(5).GetValue<string>()) ? null : row.Cell(5).GetValue<string>(),
                Biography = string.IsNullOrEmpty(row.Cell(6).GetValue<string>()) ? null : row.Cell(6).GetValue<string>(),
                Generation = row.Cell(7).GetValue<int>(),
                BirthOrder = row.Cell(8).GetValue<int>()
            };

            // Try to parse FatherId
            var fatherIdStr = row.Cell(9).GetValue<string>();
            if (!string.IsNullOrEmpty(fatherIdStr) && int.TryParse(fatherIdStr, out var fatherId))
            {
                member.FatherId = fatherId;
            }

            members.Add(member);
        }

        return members;
    }

    public async Task ExportToExcelAsync(List<FamilyMember> members, string filePath)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("族谱成员");

        // Header row
        worksheet.Cell(1, 1).Value = "姓名";
        worksheet.Cell(1, 2).Value = "性别";
        worksheet.Cell(1, 3).Value = "出生日期";
        worksheet.Cell(1, 4).Value = "逝世日期";
        worksheet.Cell(1, 5).Value = "配偶姓名";
        worksheet.Cell(1, 6).Value = "生平事迹";
        worksheet.Cell(1, 7).Value = "辈分";
        worksheet.Cell(1, 8).Value = "排行";
        worksheet.Cell(1, 9).Value = "父亲ID";

        // Style header
        var headerRange = worksheet.Range(1, 1, 1, 9);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Data rows
        int row = 2;
        foreach (var member in members)
        {
            worksheet.Cell(row, 1).Value = member.Name;
            worksheet.Cell(row, 2).Value = member.Gender == Gender.Male ? "男" : "女";
            worksheet.Cell(row, 3).Value = member.BirthDate;
            worksheet.Cell(row, 4).Value = member.DeathDate ?? "";
            worksheet.Cell(row, 5).Value = member.SpouseName ?? "";
            worksheet.Cell(row, 6).Value = member.Biography ?? "";
            worksheet.Cell(row, 7).Value = member.Generation;
            worksheet.Cell(row, 8).Value = member.BirthOrder;
            worksheet.Cell(row, 9).Value = member.FatherId?.ToString() ?? "";
            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        await Task.Run(() => workbook.SaveAs(filePath));
    }
}
