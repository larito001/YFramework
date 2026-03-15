using System.Collections.Generic;

public class BuildInventoryService : IGameService, IInventoryService
{
    private readonly Dictionary<string, int> _currency = new Dictionary<string, int>();

    public bool Has(string currency, int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        return _currency.TryGetValue(currency, out var current) && current >= amount;
    }

    public bool TrySpend(string currency, int amount)
    {
        if (!Has(currency, amount))
        {
            return false;
        }

        _currency[currency] -= amount;
        return true;
    }

    public void Add(string currency, int amount)
    {
        if (string.IsNullOrEmpty(currency) || amount == 0)
        {
            return;
        }

        _currency.TryGetValue(currency, out var current);
        _currency[currency] = current + amount;
    }

    public int GetBalance(string currency)
    {
        return _currency.TryGetValue(currency, out var current) ? current : 0;
    }

    public void Init(GameContext ctx)
    {
        _currency.Clear();
        Add("Gold", 999);
    }

    public void Shutdown()
    {
        _currency.Clear();
    }
}
