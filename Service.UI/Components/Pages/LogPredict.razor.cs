// "Một sản phẩm từ phòng sharepoint. SIMAX-CôngVM"

using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using Service.Share.Contract.Queries;
using Service.Shared.Commons.Enums;
using Service.Shared.Commons.Interfaces.Extentions;
using Service.Shared.Commons.Model.PHANQUYEN;
using Service.Shared.Commons.Model.ServiceCustomHttpClient;
using Service.UI.Component.Shared.Confirms;
using Service.UI.Component.Shared.Grids;

namespace Service.UI.Components.Pages
{
    public partial class LogPredict : ComponentBase
    {
        [Inject] protected IToastService ToastService { get; set; } = default!;
        [Inject] protected NavigationManager NavigationManager { get; set; } = default!;
        [Inject] protected IConfiguration Configuration { get; set; } = default!;
        [Inject] protected IDialogService DialogService { get; set; } = default!;
        [Inject] protected IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private ICallServiceRegistry CallService { get; set; } = default!;

        [CascadingParameter]
        public CurrentUserDto CurrentUserDto { get; set; } = new CurrentUserDto();

        [Parameter] public string? Year { get; set; } = DateTime.Now.Year.ToString();

        private SIGrid<DiagnosisItem, DiagnosisQuery>? siGrid;

        protected int SYear { get; set; } = DateTime.Now.Year;

        public DiagnosisQuery DiagnosisQuery { get; set; } = new DiagnosisQuery
        {
            
        };

        protected ApiRequestModel RequestModel { get; set; } = default!;

        private bool ShowAdvancedSearch;
        private DiagnosisFilterModel WorkingFilter { get; set; } = new();
        private DiagnosisFilterModel ActiveFilter { get; set; } = new();

        protected readonly (string Value, string Label)[] SeverityOptions = new[]
        {
            ("0", "Bình thường"),
            ("1", "Nhẹ"),
            ("2", "Trung bình"),
            ("3", "Nặng"),
        };

        protected override void OnInitialized()
        {
            base.OnInitialized();

            if (!string.IsNullOrWhiteSpace(Year) && int.TryParse(Year, out var y))
            {
                SYear = y;
            }

            RequestModel = new ApiRequestModel
            {
                ApiService = ServicesRegistryEnum.CustomApi,
                ApiServiceCustom = Configuration["ApiSettings:BaseUrl"] ?? string.Empty,
                Endpoint = $"/api/v1/Predict/GetPaged/{SYear}",
                Method = RequestMethod.POST,
                
            };
        }

        protected void TaoChanDoanMoi()
        {
            NavigationManager.NavigateTo($"/log-du-doan/tao-moi/{SYear}");
        }

        protected void GoCreate() => NavigationManager.NavigateTo("/du-doan");

        private async Task ApplyFiltersAsync()
        {
            DiagnosisQuery.Keyword = string.IsNullOrWhiteSpace(WorkingFilter.Keyword)
                ? null
                : WorkingFilter.Keyword.Trim();

            DiagnosisQuery.FromDate = WorkingFilter.TuNgay;
            DiagnosisQuery.ToDate = WorkingFilter.DenNgay;

            if (int.TryParse(WorkingFilter.SeverityLevel, out var severity) && severity >= 0)
            {
                DiagnosisQuery.SeverityLevel = severity;
            }
            else
            {
                DiagnosisQuery.SeverityLevel = null;
            }

            if (siGrid != null)
            {
                await siGrid.RefreshAsync();
            }
        }

        private async Task Filter(string range)
        {
            DateTime today = DateTime.Today;

            switch (range)
            {
                case "thisweek":
                    int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                    var startOfWeek = today.AddDays(-diff);
                    var endOfWeek = startOfWeek.AddDays(6);

                    WorkingFilter.TuNgay = startOfWeek;
                    WorkingFilter.DenNgay = endOfWeek;
                    break;
            }

            await ApplyFiltersAsync();
        }

        private async Task ResetFiltersAsync()
        {
            var currentYear = DateTime.Now.Year;
            WorkingFilter = new DiagnosisFilterModel();
            ActiveFilter = new DiagnosisFilterModel();

            DiagnosisQuery.Keyword = string.Empty;
            DiagnosisQuery.FromDate = null;
            DiagnosisQuery.ToDate = null;
            DiagnosisQuery.SeverityLevel = null;

            SYear = currentYear;

            if (siGrid != null)
            {
                await siGrid.RefreshAsync();
            }
        }

        private async Task OnYearChanged(int value)
        {
            SYear = value;
         
            RequestModel.Endpoint = $"/api/v1/Predict/GetPaged/{SYear}";

            if (WorkingFilter.TuNgay.HasValue)
            {
                var d = WorkingFilter.TuNgay.Value;
                int day = Math.Min(d.Day, DateTime.DaysInMonth(SYear, d.Month));
                WorkingFilter.TuNgay = new DateTime(SYear, d.Month, day);
            }

            if (WorkingFilter.DenNgay.HasValue)
            {
                var d = WorkingFilter.DenNgay.Value;
                int day = Math.Min(d.Day, DateTime.DaysInMonth(SYear, d.Month));
                WorkingFilter.DenNgay = new DateTime(SYear, d.Month, day);
            }

            await ApplyFiltersAsync();
        }

        protected async Task OpenImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                ToastService.ShowWarning("Không có đường dẫn ảnh.");
                return;
            }

            try
            {
                await JSRuntime.InvokeVoidAsync("open", path, "_blank");
            }
            catch
            {
                // Fallback: mở bằng NavigationManager nếu JS interop lỗi
                NavigationManager.NavigateTo(path, forceLoad: true);
            }
        }

        protected async Task CopyIdAsync(Guid id)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", id.ToString());
                ToastService.ShowSuccess($"Đã sao chép mã: {id}");
            }
            catch
            {
                ToastService.ShowError("Không thể sao chép mã.");
            }
        }

        protected async Task OpenModalDelete(Guid id)
        {
            try
            {
                var dialog = await DialogService.ShowDialogAsync<ModalConfirm>(new DialogParameters());
                var resultDialog = await dialog.Result;

                if (resultDialog.Cancelled == false && resultDialog.Data is bool success && success)
                {
                    var apiRequest = new ApiRequestModel
                    {
                        ApiService = ServicesRegistryEnum.CustomApi,
                        ApiServiceCustom = Configuration["ApiSettings:BaseUrl"] ?? string.Empty,
                        Endpoint = $"/api/v1/Predict/Delete/{SYear}/{id}",
                        Token = "token"
                    };

                    var result = await CallService.Delete(apiRequest);
                    if (result.Status == StatusCode.OK)
                    {
                        await RefreshGrid();
                        ToastService.ShowSuccess("Xóa bản ghi thành công!");
                    }
                    else
                    {
                        ToastService.ShowError(result.Message ?? "Lỗi khi xóa bản ghi.");
                    }
                }
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Lỗi khi xóa bản ghi: {ex.Message}");
            }
        }

        protected async Task RefreshGrid()
        {
            if (siGrid != null)
            {
                await siGrid.RefreshAsync();
            }
        }

        // ===== Helper hiển thị =====
        protected static string GetSevClass(int level) => level switch
        {
            0 => "badge-normal",
            1 => "badge-mild",
            2 => "badge-moderate",
            _ => "badge-severe"
        };

        protected static string MapSeverityName(int level, string? apiName)
        {
            if (!string.IsNullOrEmpty(apiName) && !apiName.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return apiName switch
                {
                    "Healthy" => "Bình thường",
                    "Mild" => "Nhẹ",
                    "Moderate" => "Trung bình",
                    "Severe" => "Nặng",
                    _ => apiName
                };
            }
            return level switch
            {
                0 => "Bình thường",
                1 => "Nhẹ",
                2 => "Trung bình",
                _ => "Nặng"
            };
        }

        // ===== Models =====
        public class DiagnosisItem
        {
            public Guid Id { get; set; }
            public int SeverityLevel { get; set; }
            public string? SeverityLevelName { get; set; }
            public double SeverityRatio { get; set; }
            public double Confidence { get; set; }
            public string? ModelUsed { get; set; }
            public string? ImageFileName { get; set; }
            public string? ImagePath { get; set; }
            public DateTime DiagnosedAt { get; set; }
            public string? Notes { get; set; }
        }

        public class DiagnosisFilterModel
        {
            public string? Keyword { get; set; }
            public string? SeverityLevel { get; set; } = "-1";
            public DateTime? TuNgay { get; set; }
            public DateTime? DenNgay { get; set; }
            public string? Year { get; set; }
        }
        public static class YearConfig
        {
            public static List<int> DataSearchYear { get; } = GenerateYearList();

            private static List<int> GenerateYearList()
            {
                int currentYear = DateTime.Now.Year;
                int minYear = Math.Max(2016, currentYear - 10);
                List<int> years = new();

                for (int i = currentYear; i >= minYear; i--)
                {
                    years.Add(i);
                }

                return years;
            }

        }

    }
}