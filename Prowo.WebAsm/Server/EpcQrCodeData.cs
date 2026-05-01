using System.Diagnostics.CodeAnalysis;
using IbanNet;
using Prowo.WebAsm.Shared;

namespace Prowo.WebAsm.Server;

public class EpcQrCodeData
{
    private static readonly IbanValidator IbanValidator = new();

    public string Iban { get; }
    public string AccountHolder { get; }
    public decimal? Amount { get; }
    public string RemittanceInformation { get; }

    private EpcQrCodeData(string iban, string accountHolder, decimal? amount, string remittanceInformation)
    {
        Iban = iban;
        AccountHolder = accountHolder;
        Amount = amount;
        RemittanceInformation = remittanceInformation;
    }

    public static bool TryCreate(ProjectPaymentDataDto dto, bool isFullRemittanceInfo, [NotNullWhen(true)] out EpcQrCodeData? data, out string[] errors)
    {
        var errorList = new List<string>();
        var iban = dto.Iban.Replace(" ", "").ToUpperInvariant();
        var ibanValidation = IbanValidator.Validate(iban);
        if (!ibanValidation.IsValid)
            errorList.Add(ibanValidation.Error?.ErrorMessage ?? "IBAN ist ungültig");
        if (string.IsNullOrWhiteSpace(dto.AccountHolder))
            errorList.Add("Kontoinhaber darf nicht leer sein.");
        else if (dto.AccountHolder.Length > 70)
            errorList.Add("Kontoinhaber darf maximal 70 Zeichen lang sein.");
        if (dto.Amount.HasValue && dto.Amount.Value <= 0)
            errorList.Add("Betrag muss größer als null sein.");
        if (dto.Amount.HasValue && dto.Amount.Value > 999_999_999.99m)
            errorList.Add("Betrag darf 999.999.999,99 nicht überschreiten.");
        int maxRemittanceInfoLength = isFullRemittanceInfo ? 140 : 50;
        if (dto.RemittanceInformation.Length > maxRemittanceInfoLength)
            errorList.Add($"Verwendungszweck darf maximal {maxRemittanceInfoLength} Zeichen lang sein.");
        if (errorList.Count > 0)
        {
            data = null;
            errors = [.. errorList];
            return false;
        }
        data = new EpcQrCodeData(iban, dto.AccountHolder, dto.Amount, dto.RemittanceInformation);
        errors = [];
        return true;
    }
}
