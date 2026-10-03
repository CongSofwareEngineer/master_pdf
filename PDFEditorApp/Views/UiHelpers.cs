using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PDFEditorApp.Models;
using PDFEditorApp.Services;
using WpfUiMessageBox = Wpf.Ui.Controls.MessageBox;
using WpfUiMessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace PDFEditorApp.Views;

/// <summary>Hẹn giờ một lần trên luồng UI (dùng cho lớp chờ hiện trễ).</summary>
public sealed class DispatcherTimerHelper
{
    private readonly DispatcherTimer _timer;

    public DispatcherTimerHelper(TimeSpan interval, Action tick)
    {
        _timer = new DispatcherTimer { Interval = interval };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            tick();
        };
    }

    public void Restart()
    {
        _timer.Stop();
        _timer.Start();
    }

    public void Stop() => _timer.Stop();
}

/// <summary>Kết quả hỏi lưu khi đóng.</summary>
public enum SaveChoice
{
    Save,
    DontSave,
    Cancel,
}

/// <summary>Hộp thoại thông báo theo theme Fluent (WPF-UI MessageBox), chữ đa ngôn ngữ.</summary>
public static class Dialogs
{
    public static bool IsReportable(Exception ex) =>
        ex is PdfException or IOException or UnauthorizedAccessException or InvalidOperationException
            or ArgumentException or NotSupportedException or System.Runtime.InteropServices.ExternalException;

    public static string ErrorMessage(Exception ex)
    {
        var message = ex is PdfException pdfError ? Loc.Get("Error_" + pdfError.Kind) : Loc.Get("Error_Unknown");
        var detail = ex is PdfException ? ex.InnerException?.Message : ex.Message;
        return string.IsNullOrEmpty(detail) ? message : message + Environment.NewLine + Environment.NewLine + detail;
    }

    public static Task ShowErrorAsync(Exception ex) => ShowAsync(Loc.Get("Error_Title"), ErrorMessage(ex));

    public static async Task ShowAsync(string title, string message)
    {
        var box = Create(title, message);
        box.CloseButtonText = Loc.Get("Common_OK");
        await box.ShowDialogAsync();
    }

    /// <summary>Hỏi Có / Không. True nếu chọn nút chính.</summary>
    public static async Task<bool> ConfirmAsync(string title, string message, string primaryText, bool danger = false)
    {
        var box = Create(title, message);
        box.PrimaryButtonText = primaryText;
        box.PrimaryButtonAppearance = danger ? Wpf.Ui.Controls.ControlAppearance.Danger : Wpf.Ui.Controls.ControlAppearance.Primary;
        box.CloseButtonText = Loc.Get("Common_Cancel");
        return await box.ShowDialogAsync() == WpfUiMessageBoxResult.Primary;
    }

    public static async Task<SaveChoice> AskSaveAsync(string documentName)
    {
        var box = Create(Loc.Get("App_Title"), Loc.Format("Msg_SaveChanges", documentName));
        box.PrimaryButtonText = Loc.Get("Common_Save");
        box.SecondaryButtonText = Loc.Get("Common_DontSave");
        box.CloseButtonText = Loc.Get("Common_Cancel");
        return await box.ShowDialogAsync() switch
        {
            WpfUiMessageBoxResult.Primary => SaveChoice.Save,
            WpfUiMessageBoxResult.Secondary => SaveChoice.DontSave,
            _ => SaveChoice.Cancel,
        };
    }

    private static WpfUiMessageBox Create(string title, string message) => new()
    {
        Title = title,
        Content = new System.Windows.Controls.TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 },
        Owner = Application.Current.MainWindow,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
    };
}

/// <summary>Đọc ảnh (PNG, JPG, BMP, GIF, TIFF) thành <see cref="ImageData"/>; ghi JPEG. Dùng WPF nên nằm ở Views.</summary>
public static class ImageCodec
{
    public const string OpenFilter = "*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff";

    public static ImageData Load(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            using var ms = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            var isJpeg = decoder is JpegBitmapDecoder;
            RenderedPage? bitmap = null;
            if (!isJpeg)
            {
                var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                var stride = converted.PixelWidth * 4;
                var pixels = new byte[stride * converted.PixelHeight];
                converted.CopyPixels(pixels, stride, 0);
                bitmap = new RenderedPage(converted.PixelWidth, converted.PixelHeight, stride, pixels);
            }

            return new ImageData(frame.PixelWidth, frame.PixelHeight, frame.DpiX, frame.DpiY, bitmap, isJpeg ? bytes : null);
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new PdfException(PdfErrorKind.Format, ex);
        }
    }

    public static void WriteJpeg(Stream stream, RenderedPage image, int dpi)
    {
        var source = BitmapSource.Create(image.Width, image.Height, dpi, dpi, PixelFormats.Bgr32, null, image.Pixels, image.Stride);
        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(stream);
    }
}

/// <summary>In tài liệu qua hộp thoại in của Windows (raster theo DPI máy in). Xem docs/print.md.</summary>
public static class PrintHelper
{
    public static bool Print(Window? owner, PdfService pdf, string title)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        var dialog = new System.Windows.Controls.PrintDialog { UserPageRangeEnabled = true, MinPage = 1, MaxPage = (uint)pdf.PageCount };
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        var from = 0;
        var to = pdf.PageCount - 1;
        if (dialog.PageRangeSelection == System.Windows.Controls.PageRangeSelection.UserPages)
        {
            from = Math.Clamp(dialog.PageRange.PageFrom - 1, 0, to);
            to = Math.Clamp(dialog.PageRange.PageTo - 1, from, to);
        }

        var paginator = new PdfPaginator(pdf, from, to, new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight));
        var cursor = owner?.Cursor;
        try
        {
            if (owner is not null)
            {
                owner.Cursor = System.Windows.Input.Cursors.Wait;
            }

            dialog.PrintDocument(paginator, title);
            return true;
        }
        finally
        {
            if (owner is not null)
            {
                owner.Cursor = cursor;
            }
        }
    }

    private sealed class PdfPaginator(PdfService pdf, int from, int to, Size pageSize) : DocumentPaginator
    {
        private const int PrintDpi = 200;

        public override bool IsPageCountValid => true;

        public override int PageCount => to - from + 1;

        public override Size PageSize { get; set; } = pageSize;

        public override IDocumentPaginatorSource? Source => null;

        public override DocumentPage GetPage(int pageNumber)
        {
            var index = from + pageNumber;
            var info = pdf.GetPageInfo(index);
            var image = BitmapHelper.ToBitmapSource(pdf.RenderPage(index, PrintDpi / 72.0, forScreen: false), PrintDpi);
            var pageW = info.Width * 96 / 72;
            var pageH = info.Height * 96 / 72;
            var scale = Math.Min(PageSize.Width / pageW, PageSize.Height / pageH);
            var w = pageW * scale;
            var h = pageH * scale;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawImage(image, new Rect((PageSize.Width - w) / 2, (PageSize.Height - h) / 2, w, h));
            }

            Debug.WriteLine($"Print page {index + 1}");
            return new DocumentPage(visual, PageSize, new Rect(PageSize), new Rect(PageSize));
        }
    }
}
