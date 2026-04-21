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

    public static bool TryCreate(ProjectPaymentDataDto dto, [NotNullWhen(true)] out EpcQrCodeData? data, out string[] errors)
    {
        var errorList = new List<string>();
        var iban = dto.Iban.Replace(" ", "").ToUpperInvariant();
        if (!IbanValidator.Validate(iban).IsValid)
            errorList.Add("IBAN is invalid.");
        if (string.IsNullOrWhiteSpace(dto.AccountHolder))
            errorList.Add("Account holder is required.");
        else if (dto.AccountHolder.Length > 70)
            errorList.Add("Account holder must not exceed 70 characters.");
        if (dto.Amount.HasValue && dto.Amount.Value <= 0)
            errorList.Add("Amount must be greater than zero.");
        if (dto.Amount.HasValue && dto.Amount.Value > 999_999_999.99m)
            errorList.Add("Amount must not exceed 999,999,999.99.");
        if (dto.RemittanceInformation.Length > 140)
            errorList.Add("Remittance information must not exceed 140 characters.");
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
