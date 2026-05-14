namespace BranchPOS.ViewModels;

public class ReportingViewModel
{
    public List<ReportTableViewModel> Tables { get; set; } = new();
}

public class ReportTableViewModel
{
    public string Name { get; set; } = string.Empty;

    public int RowCount { get; set; }

    public List<string> Columns { get; set; } = new();

    public List<List<string>> Rows { get; set; } = new();
}
