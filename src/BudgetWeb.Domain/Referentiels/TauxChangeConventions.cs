namespace BudgetWeb.Domain.Referentiels;



/// <summary>

/// Conventions centralisées pour les taux de change (dpm.TAUX_CHANGE).

/// Une paire canonique est stockée avec orientation fixe : 1 DeviseBase = TauxReference DeviseQuote.

/// Les paires supportées et leur orientation sont définies dans dpm.PAIRE_TAUX_CHANGE (pas en code).

/// </summary>

public static class TauxChangeConventions

{

    public const string DeviseUsd = "USD";

    public const string DeviseCdf = "CDF";

    public const int DecimalesAffichageInverse = 8;



    /// <summary>Paire canonique enregistrée (orientation unique en base).</summary>

    public sealed record PaireCanonique(string DeviseBase, string DeviseQuote)

    {

        public string Cle => $"{DeviseBase}/{DeviseQuote}";

    }



    public static IReadOnlyList<PaireCanonique> ToPairesCanoniques(IEnumerable<PaireCanonique> paires)

        => paires.ToList();



    public static bool EstIdentite(string deviseA, string deviseB)

        => string.Equals(

            deviseA.Trim().ToUpperInvariant(),

            deviseB.Trim().ToUpperInvariant(),

            StringComparison.Ordinal);



    public static bool EstPaireSupportee(IReadOnlyList<PaireCanonique> paires, string deviseA, string deviseB)

    {

        var a = NormaliserCode(deviseA);

        var b = NormaliserCode(deviseB);

        if (EstIdentite(a, b)) return false;

        return paires.Any(p =>

            (p.DeviseBase == a && p.DeviseQuote == b)

            || (p.DeviseBase == b && p.DeviseQuote == a));

    }



    public static PaireCanonique ResoudrePaire(

        IReadOnlyList<PaireCanonique> paires,

        string deviseA,

        string deviseB)

    {

        if (!TryResoudrePaire(paires, deviseA, deviseB, out var paire))

        {

            throw new ArgumentException(

                $"Paire de devises non supportée : {deviseA}/{deviseB}.");

        }



        return paire;

    }



    public static bool TryResoudrePaire(

        IReadOnlyList<PaireCanonique> paires,

        string deviseA,

        string deviseB,

        out PaireCanonique paire)

    {

        paire = null!;

        var a = NormaliserCode(deviseA);

        var b = NormaliserCode(deviseB);

        if (EstIdentite(a, b)) return false;



        var found = paires.FirstOrDefault(p =>

            (p.DeviseBase == a && p.DeviseQuote == b)

            || (p.DeviseBase == b && p.DeviseQuote == a));



        if (found is null) return false;

        paire = found;

        return true;

    }



    public static bool EstOrientationCanonique(

        IReadOnlyList<PaireCanonique> paires,

        string deviseBase,

        string deviseQuote)

    {

        var b = NormaliserCode(deviseBase);

        var q = NormaliserCode(deviseQuote);

        return paires.Any(p => p.DeviseBase == b && p.DeviseQuote == q);

    }



    /// <summary>Taux affiché pour la direction demandée (inverse calculé, jamais stocké).</summary>

    public static decimal CalculerTauxDirectionnel(

        decimal tauxReference,

        string sourceDemandee,

        string cibleDemandee,

        PaireCanonique paire)

    {

        if (tauxReference <= 0m)

            throw new ArgumentException("Le taux de référence doit être strictement positif.", nameof(tauxReference));



        var source = NormaliserCode(sourceDemandee);

        var cible = NormaliserCode(cibleDemandee);



        if (source == paire.DeviseBase && cible == paire.DeviseQuote)

            return tauxReference;



        if (source == paire.DeviseQuote && cible == paire.DeviseBase)

            return 1m / tauxReference;



        throw new ArgumentException(

            $"La direction {source}/{cible} ne correspond pas à la paire canonique {paire.Cle}.");

    }



    /// <summary>Conversion vers USD via la paire canonique résolue (indépendante de l'orientation stockée).</summary>

    public static decimal ConvertirVersUsd(

        decimal montantBrut,

        string deviseSource,

        decimal tauxReference,

        PaireCanonique paire)

        => Convertir(montantBrut, deviseSource, DeviseUsd, tauxReference, paire);



    /// <summary>Formule : MontantCDF = MontantUSD × TauxReference (USD/CDF canonique).</summary>

    public static decimal ConvertirUsdVersCdf(decimal montantUsd, decimal tauxReference)

        => Convertir(

            montantUsd,

            DeviseUsd,

            DeviseCdf,

            tauxReference,

            new PaireCanonique(DeviseUsd, DeviseCdf));



    /// <summary>Conversion générique via taux de référence canonique (précision complète).</summary>

    public static decimal Convertir(

        decimal montantSource,

        string deviseSource,

        string deviseCible,

        decimal tauxReference,

        PaireCanonique paire)

    {

        if (tauxReference <= 0m)

            throw new ArgumentException("Le taux de référence doit être strictement positif.", nameof(tauxReference));



        var source = NormaliserCode(deviseSource);

        var cible = NormaliserCode(deviseCible);



        if (EstIdentite(source, cible))

            return montantSource;



        if (source == paire.DeviseBase && cible == paire.DeviseQuote)

            return montantSource * tauxReference;



        if (source == paire.DeviseQuote && cible == paire.DeviseBase)

            return montantSource / tauxReference;



        throw new ArgumentException(

            $"Conversion impossible pour {source}/{cible} avec la paire {paire.Cle}.");

    }



    /// <summary>Arrondi affichage inverse uniquement (8 décimales) — ne pas utiliser pour les calculs.</summary>

    public static decimal FormaterTauxInverseAffichage(decimal tauxReference)

        => decimal.Round(1m / tauxReference, DecimalesAffichageInverse, MidpointRounding.AwayFromZero);



    public static string NormaliserCode(string devise)

        => devise.Trim().ToUpperInvariant();

}

