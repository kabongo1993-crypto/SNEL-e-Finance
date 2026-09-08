# Architecture — Taux de change (Budget Web)

> **Règle permanente** : une paire = une orientation canonique choisie par le métier ; l'inverse directionnel est **calculé** par `ITauxChangeService`, jamais stocké.

## Source de vérité

| Élément | Rôle |
|---------|------|
| `dpm.PAIRE_TAUX_CHANGE` | **Registre métier** — orientation canonique par couple de devises |
| `dpm.TAUX_CHANGE` | Versions de taux — une ligne par version de paire canonique |
| `ITauxChangeService` | Seule API métier transversale |
| `TauxChangeRepository` | Seul accès Entity Framework à `TAUX_CHANGE` |

## Flexibilité — orientation libre (X ↔ Y)

- **Aucun pivot USD imposé** en code. L'utilisateur choisit base et cotée à la saisie.
- Exemple métier : enregistrer **1 EUR = 3 450 USD** → paire canonique `EUR/USD`, `tauxReference = 3450`.
- Le système calcule automatiquement **1 USD = 1/3450 EUR** pour les conversions inverse.
- La **première saisie** d'un couple crée l'entrée registre avec l'orientation demandée.
- Si le registre contient déjà `USD/EUR`, une saisie `EUR/USD` est **acceptée** : le taux est **inversé** (`1/taux`) et stocké dans l'orientation canonique figée. L'inverse directionnel reste calculé à la lecture.
- Devises supportées : toutes les devises **actives** du référentiel (ex. CDF, CFA, EUR, USD).

```text
Saisie utilisateur : deviseBase=EUR, deviseQuote=USD, tauxReference=3450
  → Stocké : 1 EUR = 3450 USD (ligne TAUX_CHANGE + registre EUR/USD)
  → Calculé : 1 USD = 0,0002898551… EUR (GetApplicable / Convertir)
```

## Paire canonique — sémantique

```text
DeviseBase  / DeviseQuote
Taux        = TauxReference   →  1 DeviseBase = TauxReference DeviseQuote
```

L'inverse directionnel est **calculé**, jamais stocké comme ligne séparée.

## Statut vs date d'effet

| Concept | Rôle |
|---------|------|
| **Statut** | Version courante : max **une** ligne `ACTIF` par paire canonique |
| **DateEffet** | Sélection historique : `DateEffet <= date métier` → plus récent (**sans filtre statut**) |

## Création / remplacement

```json
POST /api/v1/taux-change
{
  "deviseBase": "EUR",
  "deviseQuote": "USD",
  "tauxReference": 1.245,
  "dateEffet": "2026-09-03",
  "confirmerRemplacement": false
}
```

- **Première version** ou date **strictement postérieure** sans collision → création normale (ancien ACTIF → INACTIF).
- Si un **ACTIF** existe déjà (même date / date antérieure / collision) et `confirmerRemplacement=false` → **409** `TAUX_REMPLACEMENT_REQUIS` avec l'ancien taux.
- Si `confirmerRemplacement=true` :
  - **non utilisé** → **écrasement** de la ligne ACTIF ;
  - **déjà utilisé** → **clôture** (INACTIF) + **nouvelle ligne** ACTIF.

## API

| Méthode | Route | Description |
|---------|-------|-------------|
| GET | `/api/v1/taux-change/paires` | Paires actives du registre |
| GET | `/api/v1/taux-change` | Liste versions (canoniques) |
| GET | `/api/v1/taux-change/applicable` | Taux directionnel + `tauxReference` |
| POST | `/api/v1/taux-change` | Nouvelle version canonique |
| POST | `/api/v1/taux-change/convertir` | Conversion centralisée |

## FK opérationnelles

`FK_TauxChange` / `FK_TauxChangePaiement` pointent **toujours** vers la ligne canonique (`IdTauxChange`).
Les snapshots (`TauxConversion`, `TauxPaiement`) conservent le taux directionnel appliqué.

## Changement d'orientation métier

Pour passer de `USD/EUR` à `EUR/USD` (cas exceptionnel) :

1. Inactiver / migrer les lignes `TAUX_CHANGE` (`nouveau_taux = 1/ancien_taux`)
2. Mettre à jour `PAIRE_TAUX_CHANGE` (DeviseBase, DeviseQuote)
3. Script métier validé — **jamais automatique**

## Migration

- Registre paires : `docs/sql/OPTIONAL_paire_taux_change_registry.sql`
- Paire canonique / purge test : `docs/sql/OPTIONAL_taux_change_paire_canonique_migration.sql`

## Fichiers centraux

| Fichier | Rôle |
|---------|------|
| `BudgetWeb.Domain/Referentiels/TauxChangeConventions.cs` | Formules math, inverse |
| `BudgetWeb.Infrastructure/Repositories/PaireTauxChangeRepository.cs` | Registre paires, création orientation libre |
| `BudgetWeb.Application/Services/TauxChangeService.cs` | Logique métier |
| `BudgetWeb.Infrastructure/Repositories/TauxChangeRepository.cs` | Persistance taux |

## Charge DPM — date de référence

| Étape | Taux obligatoire ? | Date de référence |
|-------|-------------------|-------------------|
| Saisie / soumission demandeur | Non | — |
| Traitement Chargé DPM (`TraiterCharge`, billet de conversion) | Oui | **Jour du traitement** (`DateTraitementDpm`) |
| Contrôle budgétaire (imputations) | Oui | Date d'émission de la demande (inchangé) |

- Ni la date d'enregistrement, ni la date d'émission saisie par le service émetteur ne servent de référence au traitement DPM.
- Exemple : demande émise le 26/08, traitée le 30/08 → taux USD/CDF applicable au **30/08**.
- Le frontend Charge DPM prévisualise le taux avec la date du jour (fuseau local), alignée sur le backend.
