namespace BudgetWeb.Application.DpmConsultation;

/// <summary>Types de retour dérivés en lecture seule depuis action + statuts routage (Lot 3.6.3).</summary>
public static class DemandePaiementRetourType
{
    public const string N2VersN1 = "N2_N1";
    public const string N1VersDemandeur = "N1_X";
    public const string ChargeVersDemandeur = "Y_X";
    public const string JuniorVersCharge = "Z_Y";
    public const string VisaVersControle = "V_Z";
}
