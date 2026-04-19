using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Prowo.WebAsm.Server;

public class EpcQrCodeService
{
    private readonly byte[]? logoBytes;

    public EpcQrCodeService(IWebHostEnvironment env)
    {
        var logoPath = Path.Combine(env.ContentRootPath, "logo.png");
        if (File.Exists(logoPath))
            logoBytes = File.ReadAllBytes(logoPath);
    }

    public string GenerateBase64Png(string iban, string accountHolder, decimal? amount, string remittanceInformation)
    {
        var payload = BuildEpcPayload(iban, accountHolder, amount, remittanceInformation);

        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var pngQrCode = new PngByteQRCode(qrCodeData);
        var qrPngBytes = pngQrCode.GetGraphic(pixelsPerModule: 10);

        using var qrImage = Image.Load(qrPngBytes);

        if (logoBytes != null)
        {
            using var logo = Image.Load(logoBytes);
            var logoSize = qrImage.Width / 3;
            logo.Mutate(op => op.Resize(logoSize, logoSize));
            var x = (qrImage.Width - logoSize) / 2;
            var y = (qrImage.Height - logoSize) / 2;
            qrImage.Mutate(op => op.DrawImage(logo, new Point(x, y), opacity: 1f));
        }

        using var ms = new MemoryStream();
        qrImage.SaveAsPng(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static string BuildEpcPayload(string iban, string accountHolder, decimal? amount, string remittanceInformation)
    {
        var amountStr = amount.HasValue ? $"EUR{amount.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}" : "";
        return string.Join("\n", ["BCD", "002", "1", "SCT", "", accountHolder, iban, amountStr, "", "", remittanceInformation, ""]);
    }
}
