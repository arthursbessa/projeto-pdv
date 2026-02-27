using Pdv.Application.Domain;

namespace Pdv.Infrastructure.Setup;

public static class ProductSeedData
{
    public static IReadOnlyList<ProductCacheItem> Create()
    {
        var now = DateTimeOffset.UtcNow;
        return
        [
            New("7890000000001", "Arroz Tipo 1 5kg", 2799, now),
            New("7890000000002", "Feijão Carioca 1kg", 899, now),
            New("7890000000003", "Açúcar Refinado 1kg", 499, now),
            New("7890000000004", "Café Torrado 500g", 1499, now),
            New("7890000000005", "Leite Integral 1L", 479, now),
            New("7890000000006", "Óleo de Soja 900ml", 799, now),
            New("7890000000007", "Macarrão Espaguete 500g", 459, now),
            New("7890000000008", "Molho de Tomate 340g", 329, now),
            New("7890000000009", "Biscoito Recheado 120g", 269, now),
            New("7890000000010", "Refrigerante Cola 2L", 999, now),
            New("7890000000011", "Água Mineral 500ml", 229, now),
            New("7890000000012", "Sabonete 90g", 199, now),
            New("7890000000013", "Detergente 500ml", 289, now),
            New("7890000000014", "Papel Higiênico 12un", 1899, now),
            New("7890000000015", "Shampoo 350ml", 1399, now),
            New("7890000000016", "Condicionador 350ml", 1499, now),
            New("7890000000017", "Desodorante Aerosol", 1199, now),
            New("7890000000018", "Chocolate ao Leite 90g", 499, now),
            New("7890000000019", "Batata Frita 120g", 899, now),
            New("7890000000020", "Suco de Laranja 1L", 799, now)
        ];
    }

    private static ProductCacheItem New(string barcode, string description, int cents, DateTimeOffset now) => new()
    {
        ProductId = Guid.NewGuid().ToString(),
        Barcode = barcode,
        Description = description,
        PriceCents = cents,
        Active = true,
        CreatedAt = now,
        UpdatedAt = now
    };
}
