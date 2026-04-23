using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Prowo.WebAsm.Server;

public class EpcQrCodeService(byte[] logoBytes)
{
    public string GenerateBase64Png(EpcQrCodeData data)
    {
        var payload = BuildEpcPayload(data.Iban, data.AccountHolder, data.Amount, data.RemittanceInformation);

        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.H);
        var pngQrCode = new PngByteQRCode(qrCodeData);
        var qrPngBytes = pngQrCode.GetGraphic(pixelsPerModule: 10);

        using var qrImage = Image.Load(qrPngBytes);
        using var canvas = new Image<Rgba32>(qrImage.Width, qrImage.Height, Color.White);
        canvas.Mutate(op => op.DrawImage(qrImage, new Point(0, 0), opacity: 1f));

        using var logo = Image.Load(logoBytes);
        var logoSize = qrImage.Width / 5;
        logo.Mutate(op => op.Resize(logoSize, logoSize));
        var x = (qrImage.Width - logoSize) / 2;
        var y = (qrImage.Height - logoSize) / 2;
        canvas.Mutate(op => op.DrawImage(logo, new Point(x, y), opacity: 1f));

        using var ms = new MemoryStream();
        canvas.SaveAsPng(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static string BuildEpcPayload(string iban, string accountHolder, decimal? amount, string remittanceInformation)
    {
        var amountStr = amount.HasValue ? $"EUR{amount.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}" : "";
        return string.Join("\n", ["BCD", "002", "1", "SCT", "", accountHolder, iban, amountStr, "", "", remittanceInformation, ""]);
    }
}
