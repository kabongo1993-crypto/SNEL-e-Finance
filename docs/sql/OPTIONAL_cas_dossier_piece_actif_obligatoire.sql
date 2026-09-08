-- =============================================================================
-- Évolution dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE : Actif + Obligatoire
-- Idempotent — conserver le comportement existant (Actif=1, Obligatoire=1).
-- =============================================================================

IF OBJECT_ID(N'dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE', N'U') IS NOT NULL
   AND COL_LENGTH(N'dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE', N'Actif') IS NULL
BEGIN
    ALTER TABLE dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE
        ADD Actif BIT NOT NULL CONSTRAINT DF_DPM_CAS_PIECE_Actif DEFAULT (1);
END;
GO

IF OBJECT_ID(N'dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE', N'U') IS NOT NULL
   AND COL_LENGTH(N'dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE', N'Obligatoire') IS NULL
BEGIN
    ALTER TABLE dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE
        ADD Obligatoire BIT NOT NULL CONSTRAINT DF_DPM_CAS_PIECE_Obligatoire DEFAULT (1);
END;
GO

-- Backfill explicite (DEFAULT couvre déjà les lignes existantes à l'ADD)
IF OBJECT_ID(N'dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE', N'U') IS NOT NULL
   AND COL_LENGTH(N'dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE', N'Actif') IS NOT NULL
   AND COL_LENGTH(N'dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE', N'Obligatoire') IS NOT NULL
BEGIN
    UPDATE dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE
    SET Actif = 1, Obligatoire = 1
    WHERE Actif = 0 AND Obligatoire = 1;
END;
GO

IF OBJECT_ID(N'dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE', N'U') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE name = N'CK_DPM_CAS_PIECE_Obligatoire_Implique_Actif'
          AND parent_object_id = OBJECT_ID(N'dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE')
    )
BEGIN
    ALTER TABLE dpm.CAS_DOSSIER_PIECE_OBLIGATOIRE
        ADD CONSTRAINT CK_DPM_CAS_PIECE_Obligatoire_Implique_Actif
        CHECK (Obligatoire = 0 OR Actif = 1);
END;
GO
