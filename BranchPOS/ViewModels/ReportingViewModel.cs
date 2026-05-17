namespace BranchPOS.ViewModels;

public class ReportingViewModel
{
    public List<ReportTableViewModel> Tables { get; set; } = new();

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 100;

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}

public class ReportTableViewModel
{
    public string Name { get; set; } = string.Empty;

    public int RowCount { get; set; }

    public List<string> Columns { get; set; } = new();

    public List<List<string>> Rows { get; set; } = new();
}
