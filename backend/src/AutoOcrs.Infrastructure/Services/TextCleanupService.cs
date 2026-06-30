using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoOcrs.Core.Models;
using Microsoft.Extensions.Logging;

namespace AutoOcrs.Infrastructure.Services;

public class TextCleanupService(ILogger<TextCleanupService> logger)
{
    /// <summary>
    /// Chuyển đổi kết quả OCR JSON (danh sách text box có tọa độ) thành markdown text sạch.
    /// Xử lý: sắp xếp theo thứ tự đọc, gộp hàng, dọn khoảng trắng/dòng trống thừa.
    /// </summary>
    public string ConvertToCleanMarkdown(string ocrResultJson)
    {
        if (string.IsNullOrWhiteSpace(ocrResultJson)) return string.Empty;

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var boxes = JsonSerializer.Deserialize<List<OcrTextBox>>(ocrResultJson, options);

            if (boxes == null || boxes.Count == 0) return string.Empty;

            // 1. Sắp xếp theo thứ tự đọc tự nhiên: trên → dưới (Y), trái → phải (X)
            // Box format: [xmin, ymin, xmax, ymax]
            var sortedBoxes = boxes
                .Where(b => !string.IsNullOrWhiteSpace(b.Text))
                .OrderBy(b => b.Box.Length >= 2 ? b.Box[1] : 0f) // Y (top)
                .ThenBy(b => b.Box.Length >= 1 ? b.Box[0] : 0f)  // X (left)
                .ToList();

            if (sortedBoxes.Count == 0) return string.Empty;

            // 2. Gộp các text box cùng hàng (tọa độ Y gần nhau)
            var lines = GroupIntoLines(sortedBoxes);

            // 3. Nối thành văn bản
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                sb.AppendLine(line);
            }

            // 4. Dọn dẹp text
            string result = sb.ToString();
            result = CleanupText(result);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Lỗi khi chuyển đổi OCR JSON sang markdown, trả về text thô.");
            return string.Empty;
        }
    }

    /// <summary>
    /// Gộp các text box có tọa độ Y gần nhau thành các dòng.
    /// </summary>
    private List<string> GroupIntoLines(List<OcrTextBox> sortedBoxes)
    {
        var lines = new List<string>();
        var currentLineBoxes = new List<OcrTextBox>();
        float currentLineY = -1f;

        foreach (var box in sortedBoxes)
        {
            float boxY = box.Box.Length >= 2 ? box.Box[1] : 0f;      // ymin
            float boxYMax = box.Box.Length >= 4 ? box.Box[3] : boxY;  // ymax
            float boxHeight = boxYMax - boxY;

            // Ngưỡng gộp hàng: nếu chênh lệch Y < 50% chiều cao box thì coi là cùng hàng
            float threshold = Math.Max(boxHeight * 0.5f, 10f);

            if (currentLineY < 0 || Math.Abs(boxY - currentLineY) <= threshold)
            {
                // Cùng hàng
                currentLineBoxes.Add(box);
                if (currentLineY < 0) currentLineY = boxY;
            }
            else
            {
                // Hàng mới → flush hàng cũ
                lines.Add(MergeLineBoxes(currentLineBoxes));
                currentLineBoxes = [box];
                currentLineY = boxY;
            }
        }

        // Flush hàng cuối
        if (currentLineBoxes.Count > 0)
        {
            lines.Add(MergeLineBoxes(currentLineBoxes));
        }

        return lines;
    }

    /// <summary>
    /// Nối các text box trong cùng một hàng (đã sắp xếp theo X) thành 1 dòng text.
    /// </summary>
    private string MergeLineBoxes(List<OcrTextBox> lineBoxes)
    {
        // Sắp xếp lại theo X (trái → phải) trong cùng hàng
        var sorted = lineBoxes.OrderBy(b => b.Box.Length >= 1 ? b.Box[0] : 0f).ToList();
        return string.Join(" ", sorted.Select(b => b.Text.Trim()));
    }

    /// <summary>
    /// Dọn dẹp text: loại bỏ khoảng trắng thừa, dòng trống thừa, trim đầu/cuối.
    /// </summary>
    private string CleanupText(string text)
    {
        // Loại bỏ nhiều dấu cách liên tiếp → 1 dấu cách (trên từng dòng)
        text = Regex.Replace(text, @"[ \t]+", " ");

        // Trim đầu/cuối mỗi dòng
        var lines = text.Split('\n').Select(l => l.Trim());
        text = string.Join("\n", lines);

        // Loại bỏ nhiều dòng trống liên tiếp → tối đa 1 dòng trống
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        // Trim toàn bộ đầu/cuối
        text = text.Trim();

        return text;
    }
}
