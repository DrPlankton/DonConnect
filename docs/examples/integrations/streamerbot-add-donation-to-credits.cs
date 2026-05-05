using System;
using System.Collections.Generic;

public class CPHInline
{
    public bool Execute()
    {
        string donorName;
        string amount;
        string currency;
        string platform;
        string message;
        string creditsSection;

        CPH.TryGetArg("donorName", out donorName);
        CPH.TryGetArg("amount", out amount);
        CPH.TryGetArg("currency", out currency);
        CPH.TryGetArg("platform", out platform);
        CPH.TryGetArg("message", out message);
        CPH.TryGetArg("creditsSection", out creditsSection);

        donorName = Clean(donorName, "Аноним");
        amount = Clean(amount, "");
        currency = Clean(currency, "");
        platform = Clean(platform, "Донат");
        message = Clean(message, "");
        creditsSection = Clean(creditsSection, "Донаты");

        string amountText = string.IsNullOrWhiteSpace(amount)
            ? ""
            : string.IsNullOrWhiteSpace(currency) ? amount : amount + " " + currency;

        string payload = "{"
            + "\"name\":\"" + EscapeJson(donorName) + "\","
            + "\"platform\":\"" + EscapeJson(platform) + "\","
            + "\"amount\":\"" + EscapeJson(amountText) + "\","
            + "\"message\":\"" + EscapeJson(message) + "\""
            + "}";

        CPH.AddToCredits(creditsSection, payload, true);
        CPH.LogInfo("Added donation to credits: " + donorName + " / " + amountText + " / " + platform);

        return true;
    }

    private string Clean(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim();
    }

    private string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}
