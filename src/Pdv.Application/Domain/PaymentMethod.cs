namespace Pdv.Application.Domain;

public enum PaymentMethod
{
    Cash = 1,
    CreditCard = 2,
    Card = CreditCard,
    DebitCard = 3,
    Pix = 4
}
