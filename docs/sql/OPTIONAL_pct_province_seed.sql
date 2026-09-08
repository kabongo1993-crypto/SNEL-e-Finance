-- =============================================================================
-- OPTIONAL — Seed officiel pct.PROVINCE depuis Trésorerie/PROVINCE.xlsx
-- Source métier : IDT_PROVINCE + Libelle. Idempotent (MERGE).
-- La colonne « N° Enr. » est ignorée. Aucun identifiant inventé.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'pct.PROVINCE', N'U') IS NULL
BEGIN
    RAISERROR(N'pct.PROVINCE introuvable. Exécutez OPTIONAL_pct_province_id_varchar.sql.', 16, 1);
    RETURN;
END;

BEGIN TRAN;

MERGE pct.PROVINCE AS t
USING (VALUES
    (N'BANDUNDU', N'BANDUNDU'),
    (N'BENI', N'BENI'),
    (N'BOM', N'BOMA'),
    (N'BUK', N'BUKAVU'),
    (N'BUMBA', N'BUMBA'),
    (N'CONGO-CENTRAL', N'CONGO-CENTRAL'),
    (N'DEQ', N'DEQ'),
    (N'DFO', N'DFO'),
    (N'DTO', N'DTO'),
    (N'FUNGURUME', N'FUNGURUME'),
    (N'GEMENA', N'GEMENA'),
    (N'GOM', N'GOMA'),
    (N'HK', N'HAUT KATANGA'),
    (N'INKI', N'INKISI'),
    (N'ISIRO', N'ISIRO'),
    (N'KAL', N'KALEMIE'),
    (N'KAMINA', N'KAMINA'),
    (N'KANAN', N'KANANGA'),
    (N'KASENGA', N'KASENGA'),
    (N'KASI', N'KASINDI'),
    (N'KASU', N'KASUMBALESA'),
    (N'KIKWIT', N'KIKWIT'),
    (N'KIMP', N'KIMPESE'),
    (N'KIN', N'KINSHASA'),
    (N'KIND', N'KINDU'),
    (N'KIP', N'KIPUSHI'),
    (N'KISA', N'KISANGANI'),
    (N'KOLW', N'KOLWEZI'),
    (N'KWLG', N'KWILU-NGONGO'),
    (N'LIK', N'LIKASI'),
    (N'LSHI', N'LUBUMBASHI'),
    (N'LUKALA', N'LUKALA'),
    (N'MANIEMA', N'MANIEMA'),
    (N'MAT', N'MATADI'),
    (N'MATETE', N'MATETE'),
    (N'MBA-NG', N'MBANZA-NGUNGU'),
    (N'MBAN', N'MBANDAKA'),
    (N'MBJ', N'MBUJIMAYI'),
    (N'MOA', N'MOANDA'),
    (N'MOK', N'MOKAMBO'),
    (N'NBADOLITE', N'GBADOLITE'),
    (N'NLEMBA', N'NLEMBA'),
    (N'PWET', N'PUETO'),
    (N'SAK', N'SAKANIA'),
    (N'TSHELA', N'TSHELA'),
    (N'UV', N'UVIRA')
) AS s (IdProvince, Libelle)
ON t.IdProvince = s.IdProvince
WHEN NOT MATCHED THEN
    INSERT (IdProvince, Libelle, Actif)
    VALUES (s.IdProvince, s.Libelle, 1)
WHEN MATCHED AND (
        t.Libelle COLLATE Latin1_General_CS_AS <> s.Libelle COLLATE Latin1_General_CS_AS
        OR t.Actif <> 1
    ) THEN
    UPDATE SET Libelle = s.Libelle, Actif = 1, DateModification = SYSUTCDATETIME();

COMMIT TRAN;

PRINT N'OPTIONAL_pct_province_seed : OK';
SELECT COUNT(*) AS NbProvinces FROM pct.PROVINCE;
SELECT IdProvince, Libelle, Actif FROM pct.PROVINCE ORDER BY IdProvince;
